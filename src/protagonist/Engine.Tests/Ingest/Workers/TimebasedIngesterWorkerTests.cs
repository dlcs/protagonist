using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using Engine.Ingest;
using Engine.Ingest.Persistence;
using Engine.Ingest.Timebased;
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
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly EngineSettings engineSettings;
    private readonly TimebasedIngesterWorker sut;

    public TimebasedIngesterWorkerTests()
    {
        assetToS3 = A.Fake<IAssetToS3>();
        mediaTranscoder = A.Fake<IMediaTranscoder>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        engineSettings = new EngineSettings();
        
        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);

        sut = new TimebasedIngesterWorker(assetToS3, optionsMonitor, mediaTranscoder, storageKeyGenerator,
            NullLogger<TimebasedIngesterWorker>.Instance);
    }

    [Fact]
    public async Task Ingest_ReturnsFailed_IfCopyAssetError()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("2/1/shallow"));
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<Asset>._, true, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .ThrowsAsync(new Exception());

        // Act
        var result = await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

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
        A.CallTo(() => assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<Asset>._, A<bool>._,
            A<CustomerOriginStrategy>._, A<CancellationToken>._)).Returns(assetFromOrigin);

        // Act
        await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

        // Assert
        A.CallTo(() =>
            assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<Asset>._, !noStoragePolicyCheck,
                A<CustomerOriginStrategy>._, A<CancellationToken>._))
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
            assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<Asset>._, A<bool>._, A<CustomerOriginStrategy>._,
                A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        var result = await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(IngestResultStatus.StorageLimitExceeded);
    }

    [Fact]
    public async Task Ingest_ReturnsQueuedForProcessing_IfMediaTranscodeSuccess()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("2/1/remurdered"));

        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<Asset>._, A<bool>._, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(new AssetFromOrigin(asset.Id, 13, "target", "application/json"));

        A.CallTo(() => mediaTranscoder.InitiateTranscodeOperation(A<IngestionContext>._, A<CancellationToken>._))
            .Returns(true);

        // Act
        var result = await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(IngestResultStatus.QueuedForProcessing);
    }
}