using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using Engine.Ingest;
using Engine.Ingest.Persistence;
using Engine.Ingest.Timebased;
using Engine.Ingest.Timebased.Transcode;
using Engine.Tests.Ingest.File;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Engine.Tests.Ingest.Workers;

public class TimebasedIngesterWorkerTests
{
    private readonly IAssetToS3 assetToS3;
    private readonly IMediaTranscoder mediaTranscoder;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly TimebasedIngesterWorker sut;

    public TimebasedIngesterWorkerTests()
    {
        assetToS3 = A.Fake<IAssetToS3>();
        mediaTranscoder = A.Fake<IMediaTranscoder>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        A.CallTo(() => storageKeyGenerator.GetTimebasedInputLocation(A<AssetId>._))
            .Returns(new ObjectInBucket("bucket", "key"));
        A.CallTo(() => storageKeyGenerator.GetStoredOriginalLocation(A<AssetId>._))
            .Returns(new RegionalisedObjectInBucket("bucket", "key", "region"));
        var assetIngestorSizeCheck = new HardcodedAssetIngestorSizeCheckBase(54);

        sut = new TimebasedIngesterWorker(assetToS3, mediaTranscoder, storageKeyGenerator, assetIngestorSizeCheck,
            NullLogger<TimebasedIngesterWorker>.Instance);
    }

    [Fact]
    public async Task Ingest_ReturnsFailed_IfCopyAssetError()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("2/1/shallow"));
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<IngestionContext>._, true, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .ThrowsAsync(new Exception());

        // Act
        var result = await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(IngestResultStatus.Failed);
    }
    
    [Theory]
    [InlineData(54, true)]
    [InlineData(10, false)]
    public async Task Ingest_SetsVerifySizeFlagCorrectly(int customerId, bool noStoragePolicyCheck)
    {
        // Arrange
        var asset = new Asset(AssetId.FromString($"{customerId}/1/shallow"));
        var assetFromOrigin = new AssetFromOrigin(asset.Id, 13, "/target/location", "application/json");
        A.CallTo(() => assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<IngestionContext>._, A<bool>._,
            A<CustomerOriginStrategy>._, A<CancellationToken>._)).Returns(assetFromOrigin);

        // Act
        await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

        // Assert
        A.CallTo(() =>
            assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<IngestionContext>._, !noStoragePolicyCheck,
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
            assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<IngestionContext>._, A<bool>._, A<CustomerOriginStrategy>._,
                A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        var result = await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(IngestResultStatus.StorageLimitExceeded);
    }

    [Fact]
    public async Task Ingest_SetsSizeValue_InMetadata_IfOriginLocationInIngestionContext()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("2/1/remurdered"));

        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<IngestionContext>._, A<bool>._, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(new AssetFromOrigin(asset.Id, 13, "target", "application/json"));

        var originLocation = new RegionalisedObjectInBucket("bucket", "key", "fake-region");
        A.CallTo(() => storageKeyGenerator.GetStoredOriginalLocation(asset.Id))
            .Returns(originLocation);

        var ingestionContext = new IngestionContext(asset)
        {
            StoredObjects = { [originLocation] = 1234L }
        };

        // Act
        await sut.Ingest(ingestionContext, new CustomerOriginStrategy());
        
        // Assert
        A.CallTo(() =>
            mediaTranscoder.InitiateTranscodeOperation(
                ingestionContext,
                A<Dictionary<string, string>>.That.Matches(d => d[UserMetadataKeys.OriginSize] == "1234"),
                A<CancellationToken>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task Ingest_ReturnsQueuedForProcessing_IfMediaTranscodeSuccess()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("2/1/remurdered"));

        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<IngestionContext>._, A<bool>._, A<CustomerOriginStrategy>._,
                    A<CancellationToken>._))
            .Returns(new AssetFromOrigin(asset.Id, 13, "target", "application/json"));

        A.CallTo(() =>
            mediaTranscoder.InitiateTranscodeOperation(A<IngestionContext>._, A<Dictionary<string, string>>._,
                A<CancellationToken>._)).Returns(true);

        // Act
        var result = await sut.Ingest(new IngestionContext(asset), new CustomerOriginStrategy());

        // Assert
        result.Should().Be(IngestResultStatus.QueuedForProcessing);
    }
}