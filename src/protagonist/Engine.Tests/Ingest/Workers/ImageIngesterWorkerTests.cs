using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using Engine.Ingest;
using Engine.Ingest.Image;
using Engine.Ingest.Image.Completion;
using Engine.Ingest.Persistence;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Workers;

public class ImageIngesterWorkerTests
{
    private readonly IAssetToDisk assetToDisk;
    private readonly IImageIngestorCompletion imageIngestorCompletion;
    private readonly FakeImageProcessor imageProcessor;
    private readonly ImageIngesterWorker sut;
    private readonly EngineSettings engineSettings;

    public ImageIngesterWorkerTests()
    {
        engineSettings = new EngineSettings
        {
            ImageIngest = new ImageIngestSettings
            {
                SourceTemplate = "{root}",
            }
        };
        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);

        assetToDisk = A.Fake<IAssetToDisk>();
        imageIngestorCompletion = A.Fake<IImageIngestorCompletion>();
        imageProcessor = new FakeImageProcessor();

        sut = new ImageIngesterWorker(assetToDisk, imageProcessor, imageIngestorCompletion, optionsMonitor,
            new NullLogger<ImageIngesterWorker>());
    }

    [Fact]
    public async Task Ingest_ReturnsFailed_IfCopyAssetError()
    {
        // Arrange
        var asset = new Asset { Id = "/2/1/shallow", Customer = 99, Space = 1 };
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(A<Asset>._, A<string>._, true, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .ThrowsAsync(new ArgumentNullException());

        // Act
        var result = await sut.Ingest(new IngestAssetRequest(asset, new DateTime()), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(IngestResultStatus.Failed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ingest_SetsVerifySizeFlag_DependingOnCustomerOverride(bool noStoragePolicyCheck)
    {
        // Arrange
        const int customerId = 54;
        var asset = new Asset { Id = "/2/1/shallow", Customer = customerId, Space = 1 };
        engineSettings.CustomerOverrides.Add(customerId.ToString(), new CustomerOverridesSettings
        {
            NoStoragePolicyCheck = noStoragePolicyCheck
        });
        var assetFromOrigin = new AssetFromOrigin(asset.GetAssetId(), 13, "/target/location", "application/json");
        A.CallTo(() => assetToDisk.CopyAssetToLocalDisk(A<Asset>._, A<string>._, A<bool>._, A<CustomerOriginStrategy>._,
                A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        await sut.Ingest(new IngestAssetRequest(asset, new DateTime()), new CustomerOriginStrategy());

        // Assert
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(A<Asset>._, A<string>._, !noStoragePolicyCheck, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task Ingest_ReturnsStorageLimitExceeded_IfFileSizeTooLarge()
    {
        // Arrange
        var asset = new Asset { Id = "/2/1/remurdered", Customer = 2, Space = 1 };
        var assetFromOrigin = new AssetFromOrigin(asset.GetAssetId(), 13, "/target/location", "application/json");
        assetFromOrigin.FileTooLarge();
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(A<Asset>._, A<string>._, true, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        var result = await sut.Ingest(new IngestAssetRequest(asset, DateTime.Now), new CustomerOriginStrategy());

        // Assert
        A.CallTo(() => imageIngestorCompletion.CompleteIngestion(A<IngestionContext>._, false, A<string>._))
            .MustHaveHappened();
        result.Should().Be(IngestResultStatus.StorageLimitExceeded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ingest_CompletesIngestion_RegardlessOfImageProcessResult(bool imageProcessSuccess)
    {
        // Arrange
        var target = $".{Path.PathSeparator}{nameof(Ingest_CompletesIngestion_RegardlessOfImageProcessResult)}";

        var asset = new Asset { Id = "/2/1/remurdered", Customer = 2, Space = 1 };

        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(A<Asset>._, A<string>._, true, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(new AssetFromOrigin(asset.GetAssetId(), 13, target, "application/json"));
        imageProcessor.ReturnValue = imageProcessSuccess;

        // Act
        await sut.Ingest(new IngestAssetRequest(asset, new DateTime()), new CustomerOriginStrategy());

        // Assert
        A.CallTo(() =>
                imageIngestorCompletion.CompleteIngestion(A<IngestionContext>._, imageProcessSuccess, A<string>._))
            .MustHaveHappened();
        imageProcessor.WasCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true, IngestResultStatus.Success)]
    [InlineData(false, true, IngestResultStatus.Failed)]
    [InlineData(true, false, IngestResultStatus.Failed)]
    public async Task Ingest_ReturnsCorrectResult_DependingOnIngestAndCompletion(bool imageProcessSuccess,
        bool completeResult, IngestResultStatus expected)
    {
        // Arrange
        var asset = new Asset { Id = "/2/1/remurdered", Customer = 2, Space = 1 };

        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(A<Asset>._, A<string>._, true, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(new AssetFromOrigin(asset.GetAssetId(), 13, "target", "application/json"));

        A.CallTo(() =>
                imageIngestorCompletion.CompleteIngestion(A<IngestionContext>._, imageProcessSuccess, A<string>._))
            .Returns(completeResult);

        imageProcessor.ReturnValue = imageProcessSuccess;

        // Act
        var result = await sut.Ingest(new IngestAssetRequest(asset, new DateTime()), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(expected);
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