using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using Engine.Ingest;
using Engine.Ingest.Completion;
using Engine.Ingest.Timebased;
using Engine.Ingest.Workers;
using Engine.Ingest.Workers.Persistence;
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
        var asset = new Asset { Id = "/2/1/shallow", Customer = 99, Space = 1 };
        A.CallTo(() =>
                assetToS3.CopyAssetToTranscodeInput(A<Asset>._, true, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .ThrowsAsync(new Exception());

        // Act
        var result = await sut.Ingest(new IngestAssetRequest(asset, new DateTime()), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(IngestResult.Failed);
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
        var asset = new Asset { Id = "/2/1/remurdered", Customer = 2, Space = 1 };
        var assetFromOrigin = new AssetFromOrigin(asset.GetAssetId(), 13, "/target/location", "application/json");
        assetFromOrigin.FileTooLarge();
        A.CallTo(() =>
            assetToS3.CopyAssetToTranscodeInput(A<Asset>._, A<bool>._, A<CustomerOriginStrategy>._,
                A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        var result = await sut.Ingest(new IngestAssetRequest(asset, DateTime.Now), new CustomerOriginStrategy());

        // Assert
        A.CallTo(() => completion.CompleteAssetInDatabase(asset, null, A<CancellationToken>._)).MustHaveHappened();
        result.Should().Be(IngestResult.StorageLimitExceeded);
    }
    
    [Fact]
    public async Task Ingest_CompletesIngestion_IfMediaTranscodeFails()
    {
        // Arrange
        var asset = new Asset { Id = "/2/1/remurdered", Customer = 2, Space = 1 };

        A.CallTo(() =>
                assetToS3.CopyAssetToTranscodeInput(A<Asset>._, A<bool>._, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(new AssetFromOrigin(asset.GetAssetId(), 13, "target", "application/json"));

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
        var asset = new Asset { Id = "/2/1/remurdered", Customer = 2, Space = 1 };

        A.CallTo(() =>
                assetToS3.CopyAssetToTranscodeInput(A<Asset>._, A<bool>._, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(new AssetFromOrigin(asset.GetAssetId(), 13, "target", "application/json"));

        A.CallTo(() => mediaTranscoder.InitiateTranscodeOperation(A<IngestionContext>._, A<CancellationToken>._))
            .Returns(true);

        // Act
        var result = await sut.Ingest(new IngestAssetRequest(asset, new DateTime()), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(IngestResult.QueuedForProcessing);
        A.CallTo(() => completion.CompleteAssetInDatabase(asset, null, A<CancellationToken>._)).MustNotHaveHappened();
    }
}