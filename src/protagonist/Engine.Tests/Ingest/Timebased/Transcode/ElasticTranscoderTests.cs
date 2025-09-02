using System.Net;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using Engine.Ingest;
using Engine.Ingest.Persistence;
using Engine.Ingest.Timebased.Transcode;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Timebased.Transcode;

public class ElasticTranscoderTests
{
    private readonly ITranscoderWrapper transcoderWrapper;
    private readonly ITranscoderPresetLookup transcoderPresetLookup;
    private readonly ElasticTranscoder sut;

    public ElasticTranscoderTests()
    {
        transcoderWrapper = A.Fake<ITranscoderWrapper>();
        transcoderPresetLookup = A.Fake<ITranscoderPresetLookup>();
        var es = new EngineSettings
        {
            TimebasedIngest = new TimebasedIngestSettings
            {
                DeliveryChannelMappings = new Dictionary<string, string>
                {
                    ["video-webm-preset"] = "Standard WebM",
                    ["video-mp4-preset"] = "Standard mp4",
                    ["audio-mp3-preset"] = "Standard audio"
                },
                PipelineName = "foo-pipeline"
            }
        };
        var engineSettings = OptionsHelpers.GetOptionsMonitor(es);

        sut = new ElasticTranscoder(transcoderWrapper, transcoderPresetLookup, engineSettings,
            NullLogger<ElasticTranscoder>.Instance);
    }

    [Fact]
    public async Task InitiateTranscodeOperation_Fail_IfPipelineIdNotFound()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("1/2/hello"));
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin());

        A.CallTo(() => transcoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns<string?>(null);

        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        asset.Error.Should().Be("Could not find ElasticTranscoder pipeline");
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task InitiateTranscodeOperation_Fail_IfPolicyDataNoExtension()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("20/10/asset-id"))
        {
            MediaType = "video/mp4",
            ImageDeliveryChannels = new List<ImageDeliveryChannel>
            {
                new()
                {
                    Channel = "iiif-av",
                    DeliveryChannelPolicy = new DeliveryChannelPolicy
                    {
                        Id = 1,
                        PolicyData = "[\"noExtensionPolicy\"]"
                    },
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
                }
            }
        };
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin());

        A.CallTo(() => transcoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns("1234567890123-abcdef");

        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        asset.Error.Should().Be("Unable to generate ElasticTranscoder outputs");
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task InitiateTranscodeOperation_Fail_IfUnableToMakesCreateJobRequest()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("20/10/asset-id"))
        {
            MediaType = "video/mp4",
            ImageDeliveryChannels = new List<ImageDeliveryChannel>
            {
                new()
                {
                    Channel = "iiif-av",
                    DeliveryChannelPolicy = new DeliveryChannelPolicy
                    {
                        Id = 1,
                        PolicyData = "[\"video-webm-preset\", \"video-mp4-preset\"]"
                    },
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
                }
            }
        };
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => transcoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns("1234567890123-abcdef");
        
        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        asset.Error.Should().Be("Unable to generate ElasticTranscoder outputs");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InitiateTranscodeOperation_MakesCreateJobRequest()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("20/10/asset-id")) {
            MediaType = "video/mp4", 
            ImageDeliveryChannels = new List<ImageDeliveryChannel> 
            {
                new()
                {
                    Channel = "iiif-av",
                    DeliveryChannelPolicy = new DeliveryChannelPolicy
                    {
                        Id = 1,
                        PolicyData = "[\"video-webm-preset\", \"video-mp4-preset\"]"
                    },
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
                }
            }
        };

        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => transcoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns("1234567890123-abcdef");

        A.CallTo(() => transcoderPresetLookup.GetPresetLookupByPolicyName())
            .Returns(new Dictionary<string, TranscoderPreset>()
            {
                ["Standard WebM"] = new ("1111111111111-aaaaaa", "Standard WebM", ""),
                ["Standard mp4"] = new ("1111111111111-aaaaab", "Standard mp4", ""),
                ["auto-preset"] = new ("9999999999999-bbbbbb", "auto-preset", "")
            });

        Dictionary<string, string>? metadata = null;
        List<CreateJobOutput>? outputs = null;
        string pipeLineId = string.Empty;
        string inputKey = string.Empty;
        A.CallTo(() => transcoderWrapper.CreateJob(A<string>._, A<string>._, A<List<CreateJobOutput>>._, A<Dictionary<string, string>>._, A<CancellationToken>._))
            .Invokes((string key, string pipeline, List<CreateJobOutput> outs, Dictionary<string, string> md,
                CancellationToken _) =>
            {
                outputs = outs;
                pipeLineId = pipeline;
                inputKey = key;
                metadata = md;
            })
            .Returns(new CreateJobResponse
                { HttpStatusCode = HttpStatusCode.Accepted, Job = new Job { Id = "1234567890123-abcdef" } });

        // Act
        await sut.InitiateTranscodeOperation(context, new Dictionary<string, string> { ["test"] = "anything" });

        // Assert
        pipeLineId.Should().Be("1234567890123-abcdef");
        inputKey.Should().Be("s3://loc/ation");
        outputs[0].Key.Should().EndWith("20/10/asset-id/full/full/max/max/0/default.webm");
        outputs[1].Key.Should().EndWith("20/10/asset-id/full/full/max/max/0/default.mp4");
        metadata.Should()
            .ContainKeys(new[] { "jobId", "startTime", "test" }, "jobId + startTime added, rest persisted");

    }
    
    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task InitiateTranscodeOperation_ReturnsFalseAndSetsError_IfErrorStatusCodeFromET(HttpStatusCode statusCode)
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("20/10/asset-id"))
        {
            MediaType = "video/mp4",
            ImageDeliveryChannels = new List<ImageDeliveryChannel>
            {
                new()
                {
                    Channel = "iiif-av",
                    DeliveryChannelPolicy = new DeliveryChannelPolicy
                    {
                        Id = 1,
                        PolicyData = "[\"video-mp4-preset\"]"
                    },
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
                } 
            }
        };
        
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => transcoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns("1234567890123-abcdef");

        A.CallTo(() => transcoderPresetLookup.GetPresetLookupByPolicyName())
            .Returns(new Dictionary<string, TranscoderPreset>()
            {
                ["Standard mp4"] = new ("1111111111111-aaaaab", "Standard mp4", ""),
                ["auto-preset"] = new ("9999999999999-bbbbbb", "auto-preset", "")
            });

        A.CallTo(() => transcoderWrapper.CreateJob(A<string>._, A<string>._,
                A<List<CreateJobOutput>>._, A<Dictionary<string, string>>._, A<CancellationToken>._))
            .Returns(new CreateJobResponse { HttpStatusCode = statusCode, Job = new Job() });

        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        result.Should().BeFalse();
        asset.Error.Should().NotBeNullOrEmpty();

        A.CallTo(() => transcoderWrapper.PersistJobId(A<AssetId>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    public async Task InitiateTranscodeOperation_ReturnsTrue_IfSuccessStatusCodeFromET(HttpStatusCode statusCode)
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("20/10/asset-id"))
        {
            MediaType = "video/mp4",
            ImageDeliveryChannels = new List<ImageDeliveryChannel>
            {
                new()
                {
                    Channel = "iiif-av",
                    DeliveryChannelPolicy = new DeliveryChannelPolicy
                    {
                        Id = 1,
                        PolicyData = "[\"video-mp4-preset\"]"
                    },
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
                } 
            }
        };

        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 123, "s3://loc/ation", "video/mpeg"));

        var elasticTranscoderJobId = "1234567890123-abcdef";
        A.CallTo(() => transcoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns(elasticTranscoderJobId);

        A.CallTo(() => transcoderPresetLookup.GetPresetLookupByPolicyName())
            .Returns(new Dictionary<string, TranscoderPreset>
            {
                ["Standard mp4"] = new ("1111111111111-aaaaab", "Standard mp4", ""),
                ["auto-preset"] = new ("9999999999999-bbbbbb", "auto-preset", "")
            });

        A.CallTo(() => transcoderWrapper.CreateJob(A<string>._, A<string>._,
                A<List<CreateJobOutput>>._, A<Dictionary<string, string>>._, A<CancellationToken>._))
            .Returns(new CreateJobResponse
                { HttpStatusCode = statusCode, Job = new Job { Id = elasticTranscoderJobId } });

        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        result.Should().BeTrue();
        asset.Error.Should().BeNullOrEmpty();

        A.CallTo(() => transcoderWrapper.PersistJobId(
                A<AssetId>.That.Matches(a => a == asset.Id),
                elasticTranscoderJobId,
                A<CancellationToken>._))
            .MustHaveHappened();
    }
}
