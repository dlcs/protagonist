using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using Engine.Ingest;
using Engine.Ingest.Image;
using Engine.Ingest.Image.Completion;
using Engine.Ingest.Persistence;
using Engine.Settings;
using Engine.Tests.Ingest.File;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Workers;

public class ImageIngesterWorkerTests
{
    private readonly IAssetToDisk assetToDisk;
    private readonly FakeImageProcessor imageProcessor;
    private readonly ImageIngesterWorker sut;
    private readonly IImageIngestPostProcessing imageIngestPostProcessing;

    public ImageIngesterWorkerTests()
    {
        var assetIngestorSizeCheck = new HardcodedAssetIngestorSizeCheckBase(54);
        var engineSettings = new EngineSettings
        {
            ImageIngest = new ImageIngestSettings
            {
                SourceTemplate = "{root}",
                OrchestrateImageAfterIngest = true,
                ScratchRoot = "scratch/"
            },
        };
        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);

        assetToDisk = A.Fake<IAssetToDisk>();
        imageProcessor = new FakeImageProcessor();
        imageIngestPostProcessing = A.Fake<IImageIngestPostProcessing>();

        sut = new ImageIngesterWorker(assetToDisk, imageProcessor, optionsMonitor, assetIngestorSizeCheck,
            imageIngestPostProcessing, new NullLogger<ImageIngesterWorker>());
    }

    [Fact]
    public async Task Ingest_ReturnsFailed_IfCopyAssetError()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("2/1/shallow"));
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(A<IngestionContext>._, A<string>._, true, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .ThrowsAsync(new ArgumentNullException());

        // Act
        var result = await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(IngestResultStatus.Failed);
    }

    [Theory]
    [InlineData(54, true)]
    [InlineData(10, false)]
    public async Task Ingest_SetsVerifySizeFlag_DependingOnCustomerOverride(int customerId, bool noStoragePolicyCheck)
    {
        // Arrange
        var asset = new Asset(AssetId.FromString($"{customerId}/1/shallow"));
        var assetFromOrigin = new AssetFromOrigin(asset.Id, 13, "/target/location", "application/json");
        A.CallTo(() => assetToDisk.CopyAssetToLocalDisk(A<IngestionContext>._, A<string>._, A<bool>._, A<CustomerOriginStrategy>._,
                A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

        // Assert
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(A<IngestionContext>._, A<string>._, !noStoragePolicyCheck, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task Ingest_ReturnsStorageLimitExceeded_IfFileSizeTooLarge()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("/2/1/remurdered"));
        var assetFromOrigin = new AssetFromOrigin(asset.Id, 13, "/target/location", "application/json");
        assetFromOrigin.FileTooLarge();
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(A<IngestionContext>._, A<string>._, true, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        var result = await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(IngestResultStatus.StorageLimitExceeded);
    }

    [Theory]
    [InlineData(true, IngestResultStatus.Success)]
    [InlineData(false, IngestResultStatus.Failed)]
    public async Task Ingest_ReturnsCorrectResult_DependingOnIngestAndCompletion(bool imageProcessSuccess,
        IngestResultStatus expected)
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("/2/1/remurdered"));

        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(A<IngestionContext>._, A<string>._, true, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(new AssetFromOrigin(asset.Id, 13, "target", "application/json"));

        imageProcessor.ReturnValue = imageProcessSuccess;

        // Act
        var result = await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PostIngest_CallsImageIngestorCompletion(bool success)
    {
        // Arrange
        var assetId = AssetId.FromString("/2/1/faithless");
        var asset = new Asset(assetId);
        var ingestionContext = new IngestionContext(asset);
        
        // Act
        await sut.PostIngest(ingestionContext, success);
        
        // Assert
        A.CallTo(() => imageIngestPostProcessing.CompleteIngestion(ingestionContext, success))
            .MustHaveHappened();
    }

    

    public class FakeImageProcessor : IImageProcessor
    {
        public bool WasCalled { get; private set; }

        public bool ReturnValue { get; set; }

        public Action<IngestionContext> Callback { get; set; }

        public Task<bool> ProcessImage(IngestionContext context)
        {
            WasCalled = true;

            Callback?.Invoke(context);

            return Task.FromResult(ReturnValue);
        }
    }
}