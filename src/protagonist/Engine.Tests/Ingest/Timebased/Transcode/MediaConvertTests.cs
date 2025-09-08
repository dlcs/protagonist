using System.Net;
using DLCS.AWS.MediaConvert.Models;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;
using DLCS.AWS.Transcoding.Models.Request;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using Engine.Ingest;
using Engine.Ingest.Persistence;
using Engine.Ingest.Timebased.Transcode;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Timebased.Transcode;

public class MediaConvertTests
{
    private readonly ITranscoderWrapper transcoderWrapper;
    private readonly ITranscoderPresetLookup transcoderPresetLookup;
    private readonly MediaConvert sut;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private const string JobId = "1234567890123-abcdef";
    private const string PipelineName = "foo-pipeline";

    public MediaConvertTests()
    {
        transcoderWrapper = A.Fake<ITranscoderWrapper>();
        transcoderPresetLookup = A.Fake<ITranscoderPresetLookup>();
        A.CallTo(() => transcoderPresetLookup.GetPresetLookupByPolicyName())
            .Returns(new Dictionary<string, TranscoderPreset>
            {
                ["video-preset"] = new("Standard WebM", "video-preset", "webm"),
                ["video-mp4-preset"] = new("Standard_HD", "video-mp4-preset", "mp4"),
                ["audio-mp3-preset"] = new("Standard_196k", "audio-mp3-preset", "mp3"),
            });
        
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        var es = new AWSSettings()
        {
            Transcode = new TranscodeSettings
            {
                QueueName = PipelineName,
                RoleArn = "arn:12345"
            }
        };
        var engineSettings = OptionsHelpers.GetOptionsMonitor(es);

        sut = new MediaConvert(transcoderWrapper, transcoderPresetLookup, storageKeyGenerator, engineSettings,
            NullLogger<MediaConvert>.Instance);
    }

    [Fact]
    public async Task InitiateTranscodeOperation_Fail_IfPipelineIdNotFound()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("1/2/hello"));
        var context = new IngestionContext(asset).WithAssetFromOrigin(new AssetFromOrigin());

        A.CallTo(() => transcoderWrapper.GetPipelineId(PipelineName, A<CancellationToken>._))
            .Returns<string?>(null);

        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        asset.Error.Should().Be("Could not find MediaConvert queue");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InitiateTranscodeOperation_Fail_IfPolicyDataNotFound()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("20/10/asset-id"))
        {
            MediaType = "video/mp4",
            ImageDeliveryChannels = GetTestDeliveryChannels("[\"not-found\"]"),
        };
        var context = new IngestionContext(asset).WithAssetFromOrigin(new AssetFromOrigin());

        A.CallTo(() => transcoderWrapper.GetPipelineId(PipelineName, A<CancellationToken>._))
            .Returns(JobId);

        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        asset.Error.Should().Be("Unable to generate MediaConvert outputs");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InitiateTranscodeOperation_MakesCreateJobRequest()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("20/10/asset-id")) {
            MediaType = "video/mp4", 
            ImageDeliveryChannels = GetTestDeliveryChannels("[\"video-preset\", \"video-mp4-preset\"]"),
        };

        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => transcoderWrapper.GetPipelineId(PipelineName, A<CancellationToken>._))
            .Returns(JobId);

        Dictionary<string, string>? metadata = null;
        MediaConvertJobGroup? output = null;
        string pipeLineId = string.Empty;
        string inputKey = string.Empty;
        A.CallTo(() => transcoderWrapper.CreateJob(A<string>._, A<string>._, A<IJobOutput>._,
                A<Dictionary<string, string>>._, A<CancellationToken>._))
            .Invokes((string key, string pipeline, IJobOutput job, Dictionary<string, string> md,
                CancellationToken _) =>
            {
                output = job as MediaConvertJobGroup;
                pipeLineId = pipeline;
                inputKey = key;
                metadata = md;
            })
            .Returns(new CreateJobResponse(JobId, HttpStatusCode.Accepted));
        A.CallTo(() => storageKeyGenerator.GetTranscodeDestinationRoot(asset.Id, A<string>._))
            .Returns(new ObjectInBucket("storage-bucket", "/random/20/10/asset-id/"));

        // Act
        await sut.InitiateTranscodeOperation(context, new Dictionary<string, string> { ["test"] = "anything" });

        // Assert
        pipeLineId.Should().Be(JobId);
        inputKey.Should().Be("s3://loc/ation");
        output.Destination.Key.Should().EndWith("20/10/asset-id/", "Output destination is prefix");
        output.Outputs.First().Should()
            .Match<MediaConvertOutput>(o => o.Extension == "webm" && o.Preset == "Standard WebM");
        output.Outputs.Last().Should()
            .Match<MediaConvertOutput>(o => o.Extension == "mp4" && o.Preset == "Standard_HD");
        metadata.Should()
            .ContainKeys(["jobId", "startTime", "test"], "jobId + startTime added, rest persisted");
    }
    
    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task InitiateTranscodeOperation_Fail_IfCreateJobRequestFail(HttpStatusCode statusCode)
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("20/10/asset-id"))
        {
            MediaType = "video/mp4",
            ImageDeliveryChannels = GetTestDeliveryChannels("[\"video-preset\", \"video-mp4-preset\"]"),
        };
        var context = new IngestionContext(asset)
            .WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => transcoderWrapper.GetPipelineId(PipelineName, A<CancellationToken>._))
            .Returns(JobId);
        A.CallTo(() => transcoderWrapper.CreateJob(A<string>._, JobId, A<IJobOutput>._,
                A<Dictionary<string, string>>._, A<CancellationToken>._))
            .Returns(new CreateJobResponse("123", statusCode));
        
        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        asset.Error.Should().Be($"Create MediaConvert job failed with status {(int)statusCode}|{statusCode}");
        result.Should().BeFalse();
    }
    
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    public async Task InitiateTranscodeOperation_ReturnsTrue_IfCreateJobSuccess(HttpStatusCode statusCode)
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("20/10/asset-id"))
        {
            MediaType = "video/mp4",
            ImageDeliveryChannels = GetTestDeliveryChannels("[\"video-preset\", \"video-mp4-preset\"]"),
        };
        var context = new IngestionContext(asset)
            .WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => transcoderWrapper.GetPipelineId(PipelineName, A<CancellationToken>._))
            .Returns(JobId);
        A.CallTo(() => transcoderWrapper.CreateJob(A<string>._, JobId, A<IJobOutput>._,
                A<Dictionary<string, string>>._, A<CancellationToken>._))
            .Returns(new CreateJobResponse("123", statusCode));
        
        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        asset.Error.Should().BeNullOrEmpty();
        result.Should().BeTrue();
    }
    
    private List<ImageDeliveryChannel> GetTestDeliveryChannels(string policyData)
        =>
        [
            new()
            {
                Channel = "iiif-av",
                DeliveryChannelPolicy = new DeliveryChannelPolicy
                {
                    Id = 1,
                    PolicyData = policyData,
                    Channel = "iiif-av"
                },
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.AvDefaultVideo
            }
        ];
}
