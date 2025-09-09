using DLCS.AWS.MediaConvert.Models;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;
using DLCS.AWS.Transcoding.Models.Job;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using Engine.Data;
using Engine.Ingest.Timebased.Completion;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Data;

namespace Engine.Tests.Ingest.Timebased.Completion;

public class TimebasedIngesterCompletionTests
{
    private readonly IEngineAssetRepository engineAssetRepository;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IBucketWriter bucketWriter;
    private readonly ITranscoderWrapper transcoderWrapper;

    public TimebasedIngesterCompletionTests()
    {
        engineAssetRepository = A.Fake<IEngineAssetRepository>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        bucketWriter = A.Fake<IBucketWriter>();
        transcoderWrapper = A.Fake<ITranscoderWrapper>();
    }

    private TimebasedIngestorCompletion GetSut()
    {
        return new TimebasedIngestorCompletion(engineAssetRepository, storageKeyGenerator, bucketWriter,
            transcoderWrapper, NullLogger<TimebasedIngestorCompletion>.Instance);
    }
    
    [Fact]
    public async Task CompleteSuccessfulIngest_False_IfAssetNotFound()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, null, A<CancellationToken>._)).Returns<Asset?>(null);
        
        // Act
        var sut = GetSut();
        var result = await sut.CompleteSuccessfulIngest(assetId, null, "job");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task CompleteSuccessfulIngest_False_IfTranscodeJobNotFound()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId);
        const string jobId = "1234";
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, null, A<CancellationToken>._)).Returns(asset);
        A.CallTo(() => transcoderWrapper.GetTranscoderJob(assetId, jobId, A<CancellationToken>._))
            .Returns<TranscoderJob?>(null);
        
        // Act
        var sut = GetSut();
        var result = await sut.CompleteSuccessfulIngest(assetId, null, jobId);
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task CompleteSuccessfulIngest_FalseAndErrorSet_IfTranscodeNotComplete()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId);
        const string jobId = "1234";
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, null, A<CancellationToken>._)).Returns(asset);
        A.CallTo(() => transcoderWrapper.GetTranscoderJob(assetId, jobId, A<CancellationToken>._))
            .Returns(new TranscoderJob { ErrorCode = 101, ErrorMessage = "Test", Status = "ERROR", Input = new() });
        
        // Act
        var sut = GetSut();
        var result = await sut.CompleteSuccessfulIngest(assetId, null, jobId);
        
        // Assert
        result.Should().BeFalse();
        asset.Error.Should().NotBeNull();
    }
    
    [Theory]
    [InlineData(LargeObjectStatus.Cancelled)]
    [InlineData(LargeObjectStatus.Error)]
    [InlineData(LargeObjectStatus.SourceNotFound)]
    [InlineData(LargeObjectStatus.Unknown)]
    public async Task CompleteSuccessfulIngest_FalseAndErrorSet_IfCopyNotFound(LargeObjectStatus status)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId);
        const string jobId = "1234";
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, 1234, A<CancellationToken>._)).Returns(asset);
        A.CallTo(() => transcoderWrapper.GetTranscoderJob(assetId, jobId, A<CancellationToken>._))
            .Returns(new TranscoderJob
            {
                Status = "COMPLETE",
                Input = new(),
                Outputs = new List<TranscoderJob.TranscoderOutput>
                {
                    new() { Duration = 100, Width = 123, Height = 234, Key = "path" }
                }
            });
        
        A.CallTo(() => bucketWriter.CopyLargeObject(A<ObjectInBucket>._, A<ObjectInBucket>._,
                A<Func<long, Task<bool>>>._, A<string?>._, A<CancellationToken>._))
            .Returns(new LargeObjectCopyResult(status));

        var sut = GetSut();

        // Act
        var result = await sut.CompleteSuccessfulIngest(assetId, 1234, jobId);

        // Assert
        result.Should().BeFalse();
        asset.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteSuccessfulIngest_SetsTranscodeMetadata_IfNoneExists()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId) { MediaType = "video/mp4" };
        const string jobId = "1234";
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, 1234, A<CancellationToken>._)).Returns(asset);
        A.CallTo(() => transcoderWrapper.GetTranscoderJob(assetId, jobId, A<CancellationToken>._))
            .Returns(new TranscoderJob
            {
                Status = "COMPLETE",
                Input = new(),
                Outputs = new List<TranscoderJob.TranscoderOutput>
                {
                    new()
                    {
                        DurationMillis = 100000, Width = 123, Height = 234, Key = "path", PresetId = "This-is-name",
                        Extension = "mp4"
                    }
                }
            });
        
        A.CallTo(() => bucketWriter.CopyLargeObject(A<ObjectInBucket>._, A<ObjectInBucket>._,
                A<Func<long, Task<bool>>>._, A<string?>._, A<CancellationToken>._))
            .Returns(new LargeObjectCopyResult(LargeObjectStatus.Success));
        A.CallTo(() => storageKeyGenerator.GetTimebasedAssetLocation(A<string>._))
            .Returns(new ObjectInBucket("bucket", "location.mp3"));
        A.CallTo(() => storageKeyGenerator.GetTimebasedOutputLocation(A<string>._))
            .Returns(new ObjectInBucket("outputbucket", "output.mp4"));
        var sut = GetSut();

        var expectedMedata =
            "[{\"l\":\"s3://bucket/location.mp3\",\"n\":\"This-is-name\",\"ex\":\"mp4\",\"mt\":\"video/mp4\",\"w\":123,\"h\":234,\"d\":100000}]";

        // Act
        await sut.CompleteSuccessfulIngest(assetId, 1234, jobId);

        // Assert
        asset.AssetApplicationMetadata.Should().HaveCount(1);
        asset.AssetApplicationMetadata.Should().ContainSingle(c => c.MetadataType == "AVTranscodes" && c.MetadataValue == expectedMedata);
    }

    [Fact]
    public async Task CompleteSuccessfulIngest_OverwritesTranscodeMetadata_IfExists()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId)
        {
            MediaType = "video/mpeg",
            AssetApplicationMetadata = new List<AssetApplicationMetadata>
            {
                new() { MetadataType = "ThumbSizes", MetadataValue = "whatever" },
                new() { MetadataType = "AVTranscodes", MetadataValue = "changed" },
            }
        };
        const string jobId = "1234";
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, 1234, A<CancellationToken>._)).Returns(asset);
        A.CallTo(() => transcoderWrapper.GetTranscoderJob(assetId, jobId, A<CancellationToken>._))
            .Returns(new TranscoderJob
            {
                Status = "COMPLETE",
                Input = new(),
                Outputs = new List<TranscoderJob.TranscoderOutput>
                {
                    new()
                    {
                        DurationMillis = 100000, Width = 123, Height = 234, Key = "path", PresetId = "This-is-name",
                        Extension = "mp4"
                    }
                }
            });
        
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, 1234, A<CancellationToken>._)).Returns(asset);
        A.CallTo(() => bucketWriter.CopyLargeObject(A<ObjectInBucket>._, A<ObjectInBucket>._,
                A<Func<long, Task<bool>>>._, A<string?>._, A<CancellationToken>._))
            .Returns(new LargeObjectCopyResult(LargeObjectStatus.Success));
        A.CallTo(() => storageKeyGenerator.GetTimebasedAssetLocation(A<string>._))
            .Returns(new ObjectInBucket("bucket", "location.mp3"));
        A.CallTo(() => storageKeyGenerator.GetTimebasedOutputLocation(A<string>._))
            .Returns(new ObjectInBucket("outputbucket", "output.mp4"));
        var sut = GetSut();

        var expectedMedata =
            "[{\"l\":\"s3://bucket/location.mp3\",\"n\":\"This-is-name\",\"ex\":\"mp4\",\"mt\":\"video/mp4\",\"w\":123,\"h\":234,\"d\":100000}]";

        // Act
        await sut.CompleteSuccessfulIngest(assetId, 1234, jobId);

        // Assert
        asset.AssetApplicationMetadata.Should().HaveCount(2);
        asset.AssetApplicationMetadata.Should().ContainSingle(c => c.MetadataType == "AVTranscodes" && c.MetadataValue == expectedMedata);
        asset.AssetApplicationMetadata.Should().ContainSingle(c => c.MetadataType == "ThumbSizes" && c.MetadataValue == "whatever");
    }
    
    [Fact]
    public async Task CompleteSuccessfulIngest_StoresOriginImageSize_IfTranscodeErrorAndSizePresent()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId) { MediaType = "video/mp4" };
        const string jobId = "1234";
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, 1234, A<CancellationToken>._)).Returns(asset);
        A.CallTo(() => transcoderWrapper.GetTranscoderJob(assetId, jobId, A<CancellationToken>._))
            .Returns(new TranscoderJob
            {
                Status = "Error",
                Input = new(),
                UserMetadata = new Dictionary<string, string> { ["storedOriginSize"] = "1234" }
            });

        ImageStorage imageStorage = new();
        A.CallTo(() => engineAssetRepository.UpdateIngestedAsset(A<Asset>._, A<ImageLocation?>._, A<ImageStorage?>._,
                true, A<CancellationToken>._))
            .Invokes((Asset _, ImageLocation? _, ImageStorage? storage, bool _, CancellationToken _) =>
                imageStorage = storage!);
        
        var sut = GetSut();

        // Act
        await sut.CompleteSuccessfulIngest(assetId, 1234, jobId);

        // Assert
        imageStorage.Size.Should().Be(1234L, "Transcode Error but 'storedOriginSize' metadata stored");
    }
    
    [Fact]
    public async Task CompleteSuccessfulIngest_StoresOriginImageSize_PlusTranscodeSizeIfSuccess()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId) { MediaType = "video/mp4" };
        const string jobId = "1234";
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, 1234, A<CancellationToken>._)).Returns(asset);
        A.CallTo(() => transcoderWrapper.GetTranscoderJob(assetId, jobId, A<CancellationToken>._))
            .Returns(new TranscoderJob
            {
                Status = "COMPLETE",
                Input = new(),
                Outputs = new List<TranscoderJob.TranscoderOutput>
                {
                    new()
                    {
                        DurationMillis = 100000, Width = 123, Height = 234, Key = "path", PresetId = "This-is-name",
                        Extension = "mp4"
                    }
                },
                UserMetadata = new Dictionary<string, string> { ["storedOriginSize"] = "1234" }
            });
        A.CallTo(() => bucketWriter.CopyLargeObject(A<ObjectInBucket>._, A<ObjectInBucket>._,
                A<Func<long, Task<bool>>>._, A<string?>._, A<CancellationToken>._))
            .Returns(new LargeObjectCopyResult(LargeObjectStatus.Success, 4444L));
        A.CallTo(() => storageKeyGenerator.GetTimebasedAssetLocation(A<string>._))
            .Returns(new ObjectInBucket("bucket", "location.mp3"));
        A.CallTo(() => storageKeyGenerator.GetTimebasedOutputLocation(A<string>._))
            .Returns(new ObjectInBucket("outputbucket", "output.mp4"));

        ImageStorage imageStorage = new();
        A.CallTo(() => engineAssetRepository.UpdateIngestedAsset(A<Asset>._, A<ImageLocation?>._, A<ImageStorage?>._,
                true, A<CancellationToken>._))
            .Invokes((Asset _, ImageLocation? _, ImageStorage? storage, bool _, CancellationToken _) =>
                imageStorage = storage!);
        
        var sut = GetSut();

        // Act
        await sut.CompleteSuccessfulIngest(assetId, 1234, jobId);

        // Assert
        imageStorage.Size.Should().Be(5678L, "Stored size is 'storedOriginSize' plus transcode size (1234 + 4444)");
    }
}
