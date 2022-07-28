using System.Net;
using Amazon.ElasticTranscoder;
using Amazon.ElasticTranscoder.Model;
using DLCS.Model.Assets;
using DLCS.Repository.Caching;
using Engine.Ingest;
using Engine.Ingest.Timebased;
using Engine.Ingest.Workers;
using Engine.Settings;
using FakeItEasy;
using LazyCache;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Timebased;

public class ElasticTranscoderTests
{
    private readonly IAmazonElasticTranscoder elasticTranscoder;
    private readonly IAppCache cache;
    private readonly IOptionsMonitor<EngineSettings> engineSettings;
    private readonly ElasticTranscoder sut;

    public ElasticTranscoderTests()
    {
        elasticTranscoder = A.Fake<IAmazonElasticTranscoder>();
        cache = new MockCachingService();
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
        engineSettings = OptionsHelpers.GetOptionsMonitor(es);

        var cacheSettings = Options.Create(new CacheSettings());

        sut = new ElasticTranscoder(elasticTranscoder, cache, engineSettings, cacheSettings,
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

        A.CallTo(() => elasticTranscoder.ListPipelinesAsync(A<ListPipelinesRequest>._, A<CancellationToken>._))
            .Returns(new ListPipelinesResponse
            {
                Pipelines = new List<Pipeline> { new() { Name = "not-whats-expected" } }
            });

        // Act
        var result = await sut.InitiateTranscodeOperation(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InitiateTranscodeOperation_MakesCreateJobRequest()
    {
        // Arrange
        var asset = new Asset { Id = "20/10/asset-id", Space = 10, Customer = 20 };
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = new[] { "Standard WebM(webm)", "auto-preset(mp4)" }
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.GetAssetId(), 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => elasticTranscoder.ListPipelinesAsync(A<ListPipelinesRequest>._, A<CancellationToken>._))
            .Returns(new ListPipelinesResponse
            {
                Pipelines = new List<Pipeline> { new() { Name = "foo-pipeline", Id = "1234567890123-abcdef" } }
            });

        A.CallTo(() => elasticTranscoder.ListPresetsAsync(A<ListPresetsRequest>._, A<CancellationToken>._))
            .Returns(new ListPresetsResponse
            {
                Presets = new List<Preset>
                {
                    new() { Name = "my-custom-preset", Id = "1111111111111-aaaaaa" },
                    new() { Name = "auto-preset", Id = "9999999999999-bbbbbb" }
                },
            });

        CreateJobRequest? requestMade = null;
        A.CallTo(() => elasticTranscoder.CreateJobAsync(A<CreateJobRequest>._, A<CancellationToken>._))
            .Invokes((CreateJobRequest request, CancellationToken _) => { requestMade = request; })
            .Returns(new CreateJobResponse { HttpStatusCode = HttpStatusCode.Accepted });

        // Act
        await sut.InitiateTranscodeOperation(context);

        // Assert
        requestMade.PipelineId.Should().Be("1234567890123-abcdef");
        requestMade.UserMetadata["dlcsId"].Should().Be("20/10/asset-id");
        requestMade.Input.Key.Should().Be("ation");
        requestMade.Outputs[0].Key.Should().Be("20/10/asset-id/full/full/max/max/0/default.webm");
        requestMade.Outputs[1].Key.Should().Be("20/10/asset-id/full/full/max/max/0/default.mp4");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.Accepted, true)]
    public async Task InitiateTranscodeOperation_ReturnsCorrectSuccess(HttpStatusCode statusCode, bool success)
    {
        // Arrange
        var asset = new Asset { Id = "20/10/asset-id", Space = 10, Customer = 20 };
        asset.WithImageOptimisationPolicy(new ImageOptimisationPolicy
        {
            TechnicalDetails = new[]{ "Standard WebM(webm)", "auto-preset(mp4)" }
        });
        var context = new IngestionContext(asset);
        context.WithAssetFromOrigin(new AssetFromOrigin(asset.GetAssetId(), 123, "s3://loc/ation", "video/mpeg"));

        A.CallTo(() => elasticTranscoder.ListPipelinesAsync(A<ListPipelinesRequest>._, A<CancellationToken>._))
            .Returns(new ListPipelinesResponse
            {
                Pipelines = new List<Pipeline> { new() { Name = "foo-pipeline", Id = "1234567890123-abcdef" } }
            });

        A.CallTo(() => elasticTranscoder.ListPresetsAsync(A<ListPresetsRequest>._, A<CancellationToken>._))
            .Returns(new ListPresetsResponse
            {
                Presets = new List<Preset>
                {
                    new() { Name = "my-custom-preset", Id = "1111111111111-aaaaaa" },
                    new() { Name = "auto-preset", Id = "9999999999999-bbbbbb" }
                },
            });

        A.CallTo(() => elasticTranscoder.CreateJobAsync(A<CreateJobRequest>._, A<CancellationToken>._))
            .Returns(new CreateJobResponse { HttpStatusCode = statusCode });

        // Act
        var result = await sut.InitiateTranscodeOperation(context);

        // Assert
        result.Should().Be(success);
    }
}