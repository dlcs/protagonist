using System.Net;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Ingest;
using Engine.Ingest.Persistence;
using Engine.Ingest.Timebased.Transcode;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Settings;
using Test.Helpers.Storage;

namespace Engine.Tests.Ingest.Timebased.Transcode;

public class ElasticTranscoderTests
{
    private readonly IElasticTranscoderWrapper elasticTranscoderWrapper;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly TestBucketWriter bucketWriter;
    private readonly ElasticTranscoder sut;

    public ElasticTranscoderTests()
    {
        bucketWriter = new TestBucketWriter();
        elasticTranscoderWrapper = A.Fake<IElasticTranscoderWrapper>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        A.CallTo(() => storageKeyGenerator.GetTimebasedMetadataLocation(A<AssetId>._))
            .ReturnsLazily((AssetId assetId) => new ObjectInBucket("bucket", $"{assetId}/metadata"));
        var es = new EngineSettings
        {
            TimebasedIngest = new TimebasedIngestSettings
            {
                TranscoderMappings = new Dictionary<string, string>
                {
                    ["Standard WebM"] = "my-custom-preset",
                },
                PipelineName = "foo-pipeline"
            }
        };
        var engineSettings = OptionsHelpers.GetOptionsMonitor(es);

        sut = new ElasticTranscoder(elasticTranscoderWrapper, bucketWriter, storageKeyGenerator, engineSettings,
            NullLogger<ElasticTranscoder>.Instance);
    }

    [Fact]
    public async Task InitiateTranscodeOperation_Fail_IfPipelineIdNotFound()
    {
        // Arrange
        var asset = new Asset { Id = "1/2/hello", Customer = 1, Space = 2 };
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = Array.Empty<string>()
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin());

        A.CallTo(() => elasticTranscoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns<string?>(null);

        // Act
        var result = await sut.InitiateTranscodeOperation(context);

        // Assert
        asset.Error.Should().Be("Could not find ElasticTranscoder pipeline");
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task InitiateTranscodeOperation_Fail_IfUnableToMakesCreateJobRequest()
    {
        // Arrange
        var asset = new Asset { Id = "20/10/asset-id", Space = 10, Customer = 20, MediaType = "video/mp4"};
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = new[] { "Standard WebM(webm)", "auto-preset(mp4)" }
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.GetAssetId(), 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => elasticTranscoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns("1234567890123-abcdef");
        
        // Act
        var result = await sut.InitiateTranscodeOperation(context);

        // Assert
        asset.Error.Should().Be("Unable to generate ElasticTranscoder outputs");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InitiateTranscodeOperation_MakesCreateJobRequest()
    {
        // Arrange
        var asset = new Asset { Id = "20/10/asset-id", Space = 10, Customer = 20, MediaType = "video/mp4" };
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = new[] { "Standard WebM(webm)", "auto-preset(mp4)" }
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.GetAssetId(), 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => elasticTranscoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns("1234567890123-abcdef");

        A.CallTo(() => elasticTranscoderWrapper.GetPresetIdLookup(A<CancellationToken>._))
            .Returns(new Dictionary<string, string>()
            {
                ["my-custom-preset"] = "1111111111111-aaaaaa",
                ["auto-preset"] = "9999999999999-bbbbbb"
            });
        
        List<CreateJobOutput>? outputs = null;
        string pipeLineId = string.Empty;
        string inputKey = string.Empty;
        A.CallTo(() => elasticTranscoderWrapper.CreateJob(A<AssetId>._, A<string>._, A<string>._,
                A<List<CreateJobOutput>>._, A<string>._, A<CancellationToken>._))
            .Invokes((AssetId _, string key, string pipeline, List<CreateJobOutput> outs, string _,
                CancellationToken _) =>
            {
                outputs = outs;
                pipeLineId = pipeline;
                inputKey = key;
            })
            .Returns(new CreateJobResponse
                { HttpStatusCode = HttpStatusCode.Accepted, Job = new Job { Id = "1234567890123-abcdef" } });

        // Act
        await sut.InitiateTranscodeOperation(context);

        // Assert
        pipeLineId.Should().Be("1234567890123-abcdef");
        inputKey.Should().Be("s3://loc/ation");
        outputs[0].Key.Should().EndWith("20/10/asset-id/full/full/max/max/0/default.webm");
        outputs[1].Key.Should().EndWith("20/10/asset-id/full/full/max/max/0/default.mp4");
    }
    
    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task InitiateTranscodeOperation_ReturnsFalseAndSetsError_IfErrorStatusCodeFromET(HttpStatusCode statusCode)
    {
        // Arrange
        var asset = new Asset { Id = "20/10/asset-id", Space = 10, Customer = 20, MediaType = "video/mp4" };
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = new[]{ "Standard WebM(webm)", "auto-preset(mp4)" }
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.GetAssetId(), 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => elasticTranscoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns("1234567890123-abcdef");

        A.CallTo(() => elasticTranscoderWrapper.GetPresetIdLookup(A<CancellationToken>._))
            .Returns(new Dictionary<string, string>()
            {
                ["my-custom-preset"] = "1111111111111-aaaaaa",
                ["auto-preset"] = "9999999999999-bbbbbb"
            });

        A.CallTo(() => elasticTranscoderWrapper.CreateJob(A<AssetId>._, A<string>._, A<string>._,
                A<List<CreateJobOutput>>._, A<string>._, A<CancellationToken>._))
            .Returns(new CreateJobResponse { HttpStatusCode = statusCode, Job = new Job() });

        // Act
        var result = await sut.InitiateTranscodeOperation(context);

        // Assert
        result.Should().BeFalse();
        asset.Error.Should().NotBeNullOrEmpty();
        bucketWriter.ShouldNotHaveKey("20/10/asset-id/metadata");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    public async Task InitiateTranscodeOperation_ReturnsTrue_IfSuccessStatusCodeFromET(HttpStatusCode statusCode)
    {
        // Arrange
        var asset = new Asset { Id = "20/10/asset-id", Space = 10, Customer = 20, MediaType = "video/mp4" };
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = new[]{ "Standard WebM(webm)", "auto-preset(mp4)" }
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.GetAssetId(), 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => elasticTranscoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns("1234567890123-abcdef");

        A.CallTo(() => elasticTranscoderWrapper.GetPresetIdLookup(A<CancellationToken>._))
            .Returns(new Dictionary<string, string>()
            {
                ["my-custom-preset"] = "1111111111111-aaaaaa",
                ["auto-preset"] = "9999999999999-bbbbbb"
            });

        A.CallTo(() => elasticTranscoderWrapper.CreateJob(A<AssetId>._, A<string>._, A<string>._,
                A<List<CreateJobOutput>>._, A<string>._, A<CancellationToken>._))
            .Returns(new CreateJobResponse
                { HttpStatusCode = statusCode, Job = new Job { Id = "1234567890123-abcdef" } });

        // Act
        var result = await sut.InitiateTranscodeOperation(context);

        // Assert
        result.Should().BeTrue();
        asset.Error.Should().BeNullOrEmpty();
        bucketWriter
            .ShouldHaveKey("20/10/asset-id/metadata")
            .WithContents(
                "<JobInProgress><ElasticTranscoderJob>1234567890123-abcdef</ElasticTranscoderJob></JobInProgress>");
    }
}