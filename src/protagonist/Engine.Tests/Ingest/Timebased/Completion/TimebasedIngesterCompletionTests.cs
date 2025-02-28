using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
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
    private readonly IElasticTranscoderPresetLookup elasticTranscoderPresetLookup;

    public TimebasedIngesterCompletionTests()
    {
        engineAssetRepository = A.Fake<IEngineAssetRepository>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        bucketWriter = A.Fake<IBucketWriter>();
        elasticTranscoderPresetLookup = A.Fake<IElasticTranscoderPresetLookup>();
    }

    private TimebasedIngestorCompletion GetSut()
    {
        return new TimebasedIngestorCompletion(engineAssetRepository, storageKeyGenerator, bucketWriter,
            elasticTranscoderPresetLookup, NullLogger<TimebasedIngestorCompletion>.Instance);
    }
    
    [Fact]
    public async Task CompleteSuccessfulIngest_False_IfAssetNotFound()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, null, A<CancellationToken>._)).Returns<Asset?>(null);
        
        // Act
        var sut = GetSut();
        var result = await sut.CompleteSuccessfulIngest(assetId, null, new TranscodeResult());
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task CompleteSuccessfulIngest_FalseAndErrorSet_IfTranscodeNotComplete()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId);
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, null, A<CancellationToken>._)).Returns(asset);
        
        // Act
        var sut = GetSut();
        var result = await sut.CompleteSuccessfulIngest(assetId,
            null,
            new TranscodeResult(new TranscodedNotification
                { Input = new JobInput(), State = "ERROR", Outputs = new List<TranscodeOutput>() }));
        
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
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, 1234, A<CancellationToken>._)).Returns(asset);
        var transcodeResult = new TranscodeResult(new TranscodedNotification
        {
            Input = new JobInput(),
            State = "COMPLETED",
            Outputs = new List<TranscodeOutput>
            {
                new() { Duration = 100, Width = 123, Height = 234, Key = "path" }
            }
        });
        A.CallTo(() => bucketWriter.CopyLargeObject(A<ObjectInBucket>._, A<ObjectInBucket>._,
                A<Func<long, Task<bool>>>._, A<string?>._, A<CancellationToken>._))
            .Returns(new LargeObjectCopyResult(status));
        
        var sut = GetSut();
        
        // Act
        var result = await sut.CompleteSuccessfulIngest(assetId, 1234, transcodeResult);

        // Assert
        result.Should().BeFalse();
        asset.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteSuccessfulIngest_SetsTranscodeMetadata_IfNoneExists()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId) { MediaType = "video/mpeg" };
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, 1234, A<CancellationToken>._)).Returns(asset);

        const string presetId = "i-am-preset";
        A.CallTo(() => elasticTranscoderPresetLookup.GetPresetLookupById(A<CancellationToken>._))
            .Returns(new Dictionary<string, TranscoderPreset>
            {
                [presetId] = new TranscoderPreset(presetId, "This-is-name", "mp4")
            });

        var transcodeResult = new TranscodeResult(new TranscodedNotification
        {
            Input = new JobInput(),
            State = "COMPLETED",
            Outputs = new List<TranscodeOutput>
            {
                new()
                {
                    Duration = 100, Width = 123, Height = 234, Key = "path", PresetId = presetId, Status = "Complete"
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
            "[{\"l\":\"s3://bucket/location.mp3\",\"n\":\"This-is-name\",\"ex\":\"mp4\",\"mt\":\"video/mp4\",\"w\":123,\"h\":234,\"d\":100}]";
        
        // Act
        await sut.CompleteSuccessfulIngest(assetId, 1234, transcodeResult);
        
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
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, 1234, A<CancellationToken>._)).Returns(asset);

        const string presetId = "i-am-preset";
        A.CallTo(() => elasticTranscoderPresetLookup.GetPresetLookupById(A<CancellationToken>._))
            .Returns(new Dictionary<string, TranscoderPreset>
            {
                [presetId] = new TranscoderPreset(presetId, "This-is-name", "mp4")
            });

        var transcodeResult = new TranscodeResult(new TranscodedNotification
        {
            Input = new JobInput(),
            State = "COMPLETED",
            Outputs = new List<TranscodeOutput>
            {
                new()
                {
                    Duration = 100, Width = 123, Height = 234, Key = "path", PresetId = presetId, Status = "Complete"
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
            "[{\"l\":\"s3://bucket/location.mp3\",\"n\":\"This-is-name\",\"ex\":\"mp4\",\"mt\":\"video/mp4\",\"w\":123,\"h\":234,\"d\":100}]";
        
        // Act
        await sut.CompleteSuccessfulIngest(assetId, 1234, transcodeResult);
        
        // Assert
        asset.AssetApplicationMetadata.Should().HaveCount(2);
        asset.AssetApplicationMetadata.Should().ContainSingle(c => c.MetadataType == "AVTranscodes" && c.MetadataValue == expectedMedata);
        asset.AssetApplicationMetadata.Should().ContainSingle(c => c.MetadataType == "ThumbSizes" && c.MetadataValue == "whatever");
    }
}
