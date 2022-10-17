using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using Engine.Ingest;
using Engine.Ingest.Persistence;
using Engine.Ingest.Timebased;
using Engine.Ingest.Timebased.Completion;
using Engine.Ingest.Timebased.Transcode;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Workers;

public class TimebasedIngesterWorkerTests
{
    private readonly IAssetToS3 assetToS3;
    private readonly IMediaTranscoder mediaTranscoder;
    private readonly ITimebasedIngestorCompletion completion;
    private readonly EngineSettings engineSettings;
    private readonly TimebasedIngesterWorker sut;

    public TimebasedIngesterWorkerTests()
    {
        assetToS3 = A.Fake<IAssetToS3>();
        mediaTranscoder = A.Fake<IMediaTranscoder>();
        completion = A.Fake<ITimebasedIngestorCompletion>();

        engineSettings = new EngineSettings();
        
        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);

        sut = new TimebasedIngesterWorker(assetToS3, optionsMonitor, mediaTranscoder, completion,
            NullLogger<TimebasedIngesterWorker>.Instance);
    }

    [Fact]
    public async Task Ingest_ReturnsFailed_IfCopyAssetError()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("2/1/shallow"));
        A.CallTo(() =>
                assetToS3.CopyAssetToTranscodeInput(A<Asset>._, true, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .ThrowsAsync(new Exception());

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
        var asset = new Asset(AssetId.FromString($"{customerId}/1/shallow"));
        engineSettings.CustomerOverrides.Add(customerId.ToString(), new CustomerOverridesSettings
        {
            NoStoragePolicyCheck = noStoragePolicyCheck
        });
        var assetFromOrigin = new AssetFromOrigin(asset.Id, 13, "/target/location", "application/json");
        A.CallTo(() => assetToS3.CopyAssetToTranscodeInput(A<Asset>._, A<bool>._, A<CustomerOriginStrategy>._,
                A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        await sut.Ingest(new IngestAssetRequest(asset, new DateTime()), new CustomerOriginStrategy());

        // Assert
        A.CallTo(() =>
                assetToS3.CopyAssetToTranscodeInput(A<Asset>._, !noStoragePolicyCheck, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task Ingest_ReturnsStorageLimitExceeded_IfFileSizeTooLarge()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("2/1/shallow"));
        var assetFromOrigin = new AssetFromOrigin(asset.Id, 13, "/target/location", "application/json");
        assetFromOrigin.FileTooLarge();
        A.CallTo(() =>
            assetToS3.CopyAssetToTranscodeInput(A<Asset>._, A<bool>._, A<CustomerOriginStrategy>._,
                A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        var result = await sut.Ingest(new IngestAssetRequest(asset, DateTime.Now), new CustomerOriginStrategy());

        // Assert
        A.CallTo(() => completion.CompleteAssetInDatabase(asset, null, A<CancellationToken>._)).MustHaveHappened();
        result.Should().Be(IngestResultStatus.StorageLimitExceeded);
    }
    
    [Fact]
    public async Task Ingest_CompletesIngestion_IfMediaTranscodeFails()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("2/1/remurdered"));

        A.CallTo(() =>
                assetToS3.CopyAssetToTranscodeInput(A<Asset>._, A<bool>._, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(new AssetFromOrigin(asset.Id, 13, "target", "application/json"));

        A.CallTo(() => mediaTranscoder.InitiateTranscodeOperation(A<IngestionContext>._, A<CancellationToken>._))
            .Returns(false);

        // Act
        await sut.Ingest(new IngestAssetRequest(asset, new DateTime()), new CustomerOriginStrategy());

        // Assert
        A.CallTo(() => completion.CompleteAssetInDatabase(asset, null, A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task Ingest_ReturnsQueuedForProcessing_AndDoesNotComplete_IfMediaTranscodeSuccess()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("2/1/remurdered"));

        A.CallTo(() =>
                assetToS3.CopyAssetToTranscodeInput(A<Asset>._, A<bool>._, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(new AssetFromOrigin(asset.Id, 13, "target", "application/json"));

        A.CallTo(() => mediaTranscoder.InitiateTranscodeOperation(A<IngestionContext>._, A<CancellationToken>._))
            .Returns(true);

        // Act
        var result = await sut.Ingest(new IngestAssetRequest(asset, new DateTime()), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(IngestResultStatus.QueuedForProcessing);
        A.CallTo(() => completion.CompleteAssetInDatabase(asset, null, A<CancellationToken>._)).MustNotHaveHappened();
    }
}