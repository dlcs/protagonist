﻿using System.Net;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
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
    private readonly IElasticTranscoderWrapper elasticTranscoderWrapper;
    private readonly ElasticTranscoder sut;

    public ElasticTranscoderTests()
    {
        elasticTranscoderWrapper = A.Fake<IElasticTranscoderWrapper>();
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

        sut = new ElasticTranscoder(elasticTranscoderWrapper, engineSettings, NullLogger<ElasticTranscoder>.Instance);
    }

    [Fact]
    public async Task InitiateTranscodeOperation_Fail_IfPipelineIdNotFound()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("1/2/hello"));
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = Array.Empty<string>()
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin());

        A.CallTo(() => elasticTranscoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns<string?>(null);

        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        asset.Error.Should().Be("Could not find ElasticTranscoder pipeline");
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task InitiateTranscodeOperation_Fail_IfUnableToMakesCreateJobRequest()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("20/10/asset-id")) { MediaType = "video/mp4" };
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = new[] { "Standard WebM(webm)", "auto-preset(mp4)" }
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => elasticTranscoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
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
        var asset = new Asset(AssetId.FromString("20/10/asset-id")) { MediaType = "video/mp4" };
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = new[] { "Standard WebM(webm)", "auto-preset(mp4)" }
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => elasticTranscoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns("1234567890123-abcdef");

        A.CallTo(() => elasticTranscoderWrapper.GetPresetIdLookup(A<CancellationToken>._))
            .Returns(new Dictionary<string, string>()
            {
                ["my-custom-preset"] = "1111111111111-aaaaaa",
                ["auto-preset"] = "9999999999999-bbbbbb"
            });

        Dictionary<string, string>? metadata = null;
        List<CreateJobOutput>? outputs = null;
        string pipeLineId = string.Empty;
        string inputKey = string.Empty;
        A.CallTo(() => elasticTranscoderWrapper.CreateJob(A<string>._, A<string>._, A<List<CreateJobOutput>>._, A<Dictionary<string, string>>._, A<CancellationToken>._))
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
        var asset = new Asset(AssetId.FromString("20/10/asset-id")) { MediaType = "video/mp4" };
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = new[]{ "Standard WebM(webm)", "auto-preset(mp4)" }
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => elasticTranscoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns("1234567890123-abcdef");

        A.CallTo(() => elasticTranscoderWrapper.GetPresetIdLookup(A<CancellationToken>._))
            .Returns(new Dictionary<string, string>()
            {
                ["my-custom-preset"] = "1111111111111-aaaaaa",
                ["auto-preset"] = "9999999999999-bbbbbb"
            });

        A.CallTo(() => elasticTranscoderWrapper.CreateJob(A<string>._, A<string>._,
                A<List<CreateJobOutput>>._, A<Dictionary<string, string>>._, A<CancellationToken>._))
            .Returns(new CreateJobResponse { HttpStatusCode = statusCode, Job = new Job() });

        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        result.Should().BeFalse();
        asset.Error.Should().NotBeNullOrEmpty();

        A.CallTo(() => elasticTranscoderWrapper.PersistJobId(A<AssetId>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    public async Task InitiateTranscodeOperation_ReturnsTrue_IfSuccessStatusCodeFromET(HttpStatusCode statusCode)
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("20/10/asset-id")) { MediaType = "video/mp4" };
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = new[]{ "Standard WebM(webm)", "auto-preset(mp4)" }
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 123, "s3://loc/ation", "video/mpeg"));

        var elasticTranscoderJobId = "1234567890123-abcdef";
        A.CallTo(() => elasticTranscoderWrapper.GetPipelineId("foo-pipeline", A<CancellationToken>._))
            .Returns(elasticTranscoderJobId);

        A.CallTo(() => elasticTranscoderWrapper.GetPresetIdLookup(A<CancellationToken>._))
            .Returns(new Dictionary<string, string>
            {
                ["my-custom-preset"] = "1111111111111-aaaaaa",
                ["auto-preset"] = "9999999999999-bbbbbb"
            });

        A.CallTo(() => elasticTranscoderWrapper.CreateJob(A<string>._, A<string>._,
                A<List<CreateJobOutput>>._, A<Dictionary<string, string>>._, A<CancellationToken>._))
            .Returns(new CreateJobResponse
                { HttpStatusCode = statusCode, Job = new Job { Id = elasticTranscoderJobId } });

        // Act
        var result = await sut.InitiateTranscodeOperation(context, new Dictionary<string, string>());

        // Assert
        result.Should().BeTrue();
        asset.Error.Should().BeNullOrEmpty();

        A.CallTo(() => elasticTranscoderWrapper.PersistJobId(
                A<AssetId>.That.Matches(a => a == asset.Id),
                elasticTranscoderJobId,
                A<CancellationToken>._))
            .MustHaveHappened();
    }
}