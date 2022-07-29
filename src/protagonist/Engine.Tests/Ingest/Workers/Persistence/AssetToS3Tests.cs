using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Storage;
using Engine.Ingest.Workers;
using Engine.Ingest.Workers.Persistence;
using Engine.Settings;
using Engine.Tests.Integration;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Workers.Persistence;

public class AssetToS3Tests
{
    private readonly IAssetToDisk assetToDisk;
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageRepository storageRepository;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IFileSystem fileSystem = new FakeFileSystem();
    private readonly EngineSettings engineSettings;
    private readonly AssetToS3 sut;

    public AssetToS3Tests()
    {
        assetToDisk = A.Fake<IAssetToDisk>();
        storageRepository = A.Fake<IStorageRepository>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        bucketWriter = A.Fake<IBucketWriter>();

        engineSettings = new EngineSettings
        {
            CustomerOverrides = new Dictionary<string, CustomerOverridesSettings>
            {
                ["99"] = new() { FullBucketAccess = true },
            },
            TimebasedIngest = new TimebasedIngestSettings
            {
                SourceTemplate = "{customer}",
            }
        };
        
        A.CallTo(() => storageKeyGenerator.GetTimebasedInputLocation(A<AssetId>._))
            .Returns(new ObjectInBucket("fantasy", "test-key"));
        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);
        sut = new AssetToS3(assetToDisk, optionsMonitor, storageRepository, bucketWriter, storageKeyGenerator,
            fileSystem, new NullLogger<AssetToS3>());
    }

    [Fact]
    public async Task CopyAsset_CopiesDirectS3ToS3_IfS3AmbientAndFullBucketAccess()
    {
        // Arrange
        var asset = new Asset
        {
            Customer = 99, Space = 1, Id = "99/1/balrog",
            Origin = "s3://eu-west-1/origin/large_file.mov"
        };
        var originStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.S3Ambient
        };

        A.CallTo(() => bucketWriter.CopyLargeObject(A<ObjectInBucket>._, A<ObjectInBucket>._,
                A<Func<long, Task<bool>>>._, false, A<CancellationToken>._))
            .Returns(new LargeObjectCopyResult(LargeObjectStatus.Success, 100));
        A.CallTo(() => storageKeyGenerator.GetTimebasedInputLocation(asset.GetAssetId()))
            .Returns(new ObjectInBucket("fantasy", "99/1/balrog/1234"));

        var ct = new CancellationToken();

        // Act
        await sut.CopyAssetToTranscodeInput(asset, true, originStrategy, ct);

        // Assert
        A.CallTo(() => bucketWriter.CopyLargeObject(
                A<ObjectInBucket>.That.Matches(o => o.ToString() == "origin:::large_file.mov"),
                A<ObjectInBucket>.That.Matches(o => o.ToString() == "fantasy:::99/1/balrog/1234"),
                A<Func<long, Task<bool>>>._, false, ct))
            .MustHaveHappened();
    }

    [Fact]
    public async Task CopyAsset_ReturnsExpected_AfterDirectS3ToS3()
    {
        // Arrange
        const string mediaType = "video/quicktime";
        const long assetSize = 1024;

        var asset = new Asset
        {
            Customer = 99, Space = 1, Id = "99/1/balrog", Origin = "s3://eu-west-1/origin/large_file.mov",
            MediaType = mediaType
        };
        var originStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.S3Ambient
        };

        A.CallTo(() => bucketWriter.CopyLargeObject(A<ObjectInBucket>._, A<ObjectInBucket>._,
                A<Func<long, Task<bool>>>._, false, A<CancellationToken>._))
            .Returns(new LargeObjectCopyResult(LargeObjectStatus.Success, assetSize));

        var expected = new AssetFromOrigin(asset.GetAssetId(), assetSize, "s3://fantasy/test-key", mediaType);

        var ct = new CancellationToken();

        // Act
        var actual = await sut.CopyAssetToTranscodeInput(asset, true, originStrategy, ct);

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task CopyAsset_ReturnsFileTooLarge_IfDirectS3ToS3CopyReturnsFileTooLarge()
    {
        // Arrange
        const string mediaType = "video/quicktime";
        const long assetSize = 1024;

        var asset = new Asset
        {
            Customer = 99, Space = 1, Id = "99/1/balrog", Origin = "s3://eu-west-1/origin/large_file.mov",
            MediaType = mediaType
        };
        var originStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.S3Ambient
        };

        A.CallTo(() => bucketWriter.CopyLargeObject(A<ObjectInBucket>._, A<ObjectInBucket>._,
                A<Func<long, Task<bool>>>._, false, A<CancellationToken>._))
            .Returns(new LargeObjectCopyResult(LargeObjectStatus.FileTooLarge, assetSize));

        var expected = new AssetFromOrigin(asset.GetAssetId(), assetSize, "s3://fantasy/test-key", mediaType);
        expected.FileTooLarge();

        var ct = new CancellationToken();

        // Act
        var actual = await sut.CopyAssetToTranscodeInput(asset, true, originStrategy, ct);

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void CopyAsset_ThrowsIfDirectCopyFails()
    {
        // Arrange
        var asset = new Asset
        {
            Customer = 99, Space = 1, Id = "99/1/balrog",
            Origin = "s3://eu-west-1/origin/large_file.mov"
        };
        var originStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.S3Ambient
        };

        A.CallTo(() => bucketWriter.CopyLargeObject(A<ObjectInBucket>._, A<ObjectInBucket>._,
                A<Func<long, Task<bool>>>._, false, A<CancellationToken>._))
            .Returns(new LargeObjectCopyResult(LargeObjectStatus.Error, 100));

        var ct = new CancellationToken();

        // Act
        Func<Task> action = () => sut.CopyAssetToTranscodeInput(asset, true, originStrategy, ct);

        // Assert
        action.Should().ThrowAsync<ApplicationException>();
    }

    [Theory]
    [InlineData(OriginStrategyType.Default, 99)]
    [InlineData(OriginStrategyType.BasicHttp, 99)]
    [InlineData(OriginStrategyType.SFTP, 99)]
    [InlineData(OriginStrategyType.S3Ambient, 90)]
    public async Task CopyAsset_CopiesToDisk_IfNotS3AmbientAndFullBucketAccess(OriginStrategyType strategy,
        int customerId)
    {
        // Arrange
        var asset = new Asset
        {
            Customer = customerId, Space = 1, Id = $"{customerId}/1/balrog",
            Origin = "s3://eu-west-1/origin/large_file.mov"
        };
        var originStrategy = new CustomerOriginStrategy
        {
            Strategy = strategy
        };
        var ct = new CancellationToken();

        var assetFromOrigin = new AssetFromOrigin();
        assetFromOrigin.FileTooLarge();
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(asset, A<string>._, true, originStrategy, A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        await sut.CopyAssetToTranscodeInput(asset, true, originStrategy, ct);

        // Assert
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(asset, A<string>._, true, originStrategy, A<CancellationToken>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task CopyAsset_DoesNotWriteToBucket_IfCopiedDiskTooLarge_IfNotS3Ambient()
    {
        // Arrange
        var asset = new Asset
        {
            Customer = 1, Space = 1, Id = "1/1/balrog",
            Origin = "s3://eu-west-1/origin/large_file.mov"
        };
        var originStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.Default
        };
        var ct = new CancellationToken();

        var assetOnDisk = new AssetFromOrigin(asset.GetAssetId(), 1234, "1", "video/mpeg");
        assetOnDisk.FileTooLarge();
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(asset, A<string>._, true, originStrategy, A<CancellationToken>._))
            .Returns(assetOnDisk);

        // Act
        var response = await sut.CopyAssetToTranscodeInput(asset, true, originStrategy, ct);

        // Assert
        A.CallTo(() => bucketWriter.WriteFileToBucket(A<ObjectInBucket>._, A<string>._, A<string>._, ct))
            .MustNotHaveHappened();
        response.FileExceedsAllowance.Should().BeTrue();
    }

    [Fact]
    public async Task CopyAsset_CopiesFromDiskToBucket_IfNotS3Ambient()
    {
        // Arrange
        var asset = new Asset
        {
            Customer = 1, Space = 1, Id = "1/1/balrog",
            Origin = "s3://eu-west-1/origin/large_file.mov"
        };
        var originStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.Default
        };
        var ct = new CancellationToken();

        var assetOnDisk = new AssetFromOrigin(asset.GetAssetId(), 1234, "1", "video/mpeg");
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(asset, A<string>._, true, originStrategy, A<CancellationToken>._))
            .Returns(assetOnDisk);

        A.CallTo(() => bucketWriter.WriteFileToBucket(A<ObjectInBucket>._, A<string>._, A<string>._, ct))
            .Returns(true);

        // Act
        await sut.CopyAssetToTranscodeInput(asset, true, originStrategy, ct);

        // Assert
        A.CallTo(() => bucketWriter.WriteFileToBucket(
                A<ObjectInBucket>.That.Matches(o => o.ToString() == "fantasy:::test-key"),
                "1",
                "video/mpeg",
                ct))
            .MustHaveHappened();
    }

    [Fact]
    public async Task CopyAsset_ReturnsExpected_AfterIndirectS3ToS3()
    {
        // Arrange
        const string mediaType = "video/mpeg";
        const long assetSize = 1024;

        var asset = new Asset
        {
            Customer = 9, Space = 1, Id = "9/1/balrog",
            Origin = "s3://eu-west-1/origin/large_file.mov",
            MediaType = mediaType
        };
        var originStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.S3Ambient
        };

        var expected = new AssetFromOrigin(asset.GetAssetId(), assetSize, "s3://fantasy/test-key", mediaType);

        var ct = new CancellationToken();
        var assetOnDisk = new AssetFromOrigin(asset.GetAssetId(), assetSize, "/on/disk", mediaType);
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(asset, A<string>._, true, originStrategy, A<CancellationToken>._))
            .Returns(assetOnDisk);

        A.CallTo(() => bucketWriter.WriteFileToBucket(A<ObjectInBucket>._, A<string>._, A<string>._, ct))
            .Returns(true);

        // Act
        var actual = await sut.CopyAssetToTranscodeInput(asset, true, originStrategy, ct);

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void CopyAsset_ThrowsIfUploadToS3Fails_IfNotS3Ambient()
    {
        // Arrange
        var asset = new Asset
        {
            Customer = 9, Space = 1, Id = "9/1/balrog",
            Origin = "s3://eu-west-1/origin/large_file.mov"
        };
        var originStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.S3Ambient
        };

        var ct = new CancellationToken();
        var assetOnDisk = new AssetFromOrigin(asset.GetAssetId(), 1234, "/on/disk", "video/mpeg");
        A.CallTo(() =>
                assetToDisk.CopyAssetToLocalDisk(asset, A<string>._, true, originStrategy, A<CancellationToken>._))
            .Returns(assetOnDisk);

        // Act
        Func<Task> action = () => sut.CopyAssetToTranscodeInput(asset, true, originStrategy, ct);

        // Assert
        action.Should().ThrowAsync<ApplicationException>();
    }
}