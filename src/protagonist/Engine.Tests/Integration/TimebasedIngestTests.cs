using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.S3;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository;
using DLCS.Repository.Strategy.Utils;
using Engine.Tests.Integration.Infrastructure;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Stubbery;
using Test.Helpers;
using Test.Helpers.Integration;
using Test.Helpers.Storage;

namespace Engine.Tests.Integration;

/// <summary>
/// Tests for asset ingestion
/// </summary>
[Trait("Category", "Integration")]
[Collection(EngineCollection.CollectionName)]
public class TimebasedIngestTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);
    private readonly DlcsContext dbContext;
    private static readonly TestBucketWriter BucketWriter = new();
    private static readonly IElasticTranscoderWrapper ElasticTranscoderWrapper = A.Fake<IElasticTranscoderWrapper>();
    private readonly ApiStub apiStub;

    public TimebasedIngestTests(ProtagonistAppFactory<Startup> appFactory, EngineFixture engineFixture)
    {
        dbContext = engineFixture.DbFixture.DbContext;
        apiStub = engineFixture.ApiStub;
        httpClient = appFactory
            .WithTestServices(services =>
            {
                // Mock out things that write to disk or read from disk
                services
                    .AddSingleton<IFileSaver, FakeFileSaver>()
                    .AddSingleton<IFileSystem, FakeFileSystem>()
                    .AddSingleton<IBucketWriter>(BucketWriter)
                    .AddSingleton<IElasticTranscoderWrapper>(ElasticTranscoderWrapper);
            })
            .WithConnectionString(engineFixture.DbFixture.ConnectionString)
            .CreateClient();
        
        // Fake http origins
        apiStub.Get("/video", (request, args) => "anything")
            .Header("Content-Type", "video/mpeg");
        apiStub.Get("/audio", (request, args) => "anything")
            .Header("Content-Type", "audio/mpeg");
        
        engineFixture.DbFixture.CleanUp();

        A.CallTo(() => ElasticTranscoderWrapper.GetPipelineId("protagonist-pipeline", A<CancellationToken>._))
            .Returns("pipeline-id-1234");
        A.CallTo(() => ElasticTranscoderWrapper.GetPresetIdLookup(A<CancellationToken>._))
            .Returns(new Dictionary<string, string>
            {
                ["System preset: Webm 720p"] = "123-123",
                ["System preset: Audio MP3 - 128k"] = "456-456"
            });
    }

    [Theory]
    [InlineData("video", "/full/full/max/max/0/default.webm")]
    [InlineData("audio", "/full/max/default.mp3")]
    public async Task IngestAsset_CreatesTranscoderJob_HttpOrigin(string type, string expectedKey)
    {
        // Arrange
        var assetId = AssetId.FromString($"99/1/{nameof(IngestAsset_CreatesTranscoderJob_HttpOrigin)}-{type}");
        const string jobId = "1234567890123-abcdef";
        
        var origin = $"{apiStub.Address}/{type}";
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            imageOptimisationPolicy: $"{type}-max", mediaType: $"{type}/mpeg", family: AssetFamily.Timebased);
        var asset = entity.Entity;
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset, DateTime.UtcNow);

        A.CallTo(() => ElasticTranscoderWrapper.CreateJob(
                assetId,
                A<string>._,
                A<string>._,
                A<List<CreateJobOutput>>._,
                A<string>._,
                A<CancellationToken>._))
            .Returns(new CreateJobResponse { HttpStatusCode = HttpStatusCode.Accepted, Job = new Job { Id = jobId } });
        
        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.Should().BeSuccessful();
        
        // S3 assets created for transcoding + a metadata file
        var outputKey = $"{assetId}{expectedKey}";
        BucketWriter
            .ShouldHaveKeyThatStartsWith(assetId.ToString())
            .ForBucket(LocalStackFixture.TimebasedInputBucketName);
        
        // ET job created
        A.CallTo(() => ElasticTranscoderWrapper.CreateJob(
                assetId,
                A<string>.That.Matches(s => s.StartsWith($"s3://{LocalStackFixture.TimebasedInputBucketName}/{assetId}")),
                "pipeline-id-1234",
                A<List<CreateJobOutput>>.That.Matches(o => o.Single().Key.EndsWith(outputKey)),
                A<string>._,
                A<CancellationToken>._))
            .MustHaveHappened();

        A.CallTo(() => ElasticTranscoderWrapper.PersistJobId(assetId, jobId, A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Theory]
    [InlineData("video", "/full/full/max/max/0/default.webm")]
    [InlineData("audio", "/full/max/default.mp3")]
    public async Task IngestAsset_ReturnsNoSuccess_IfCreateTranscoderJobFails(string type, string expectedKey)
    {
        // Arrange
        var assetId =
            AssetId.FromString($"99/1/{nameof(IngestAsset_ReturnsNoSuccess_IfCreateTranscoderJobFails)}-{type}");
        
        var origin = $"{apiStub.Address}/{type}";
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            imageOptimisationPolicy: $"{type}-max", mediaType: $"{type}/mpeg", family: AssetFamily.Timebased);
        var asset = entity.Entity;
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset, DateTime.UtcNow);

        A.CallTo(() => ElasticTranscoderWrapper.CreateJob(
                assetId,
                A<string>._,
                A<string>._,
                A<List<CreateJobOutput>>._,
                A<string>._,
                A<CancellationToken>._))
            .Returns(new CreateJobResponse { HttpStatusCode = HttpStatusCode.BadRequest, Job = new Job() });
        
        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        
        // S3 assets created for transcoding + a metadata file
        var outputKey = $"{assetId}{expectedKey}";
        BucketWriter
            .ShouldHaveKeyThatStartsWith(assetId.ToString())
            .ForBucket(LocalStackFixture.TimebasedInputBucketName);
        
        // ET job created
        A.CallTo(() => ElasticTranscoderWrapper.CreateJob(
                assetId,
                A<string>.That.Matches(s => s.StartsWith($"s3://{LocalStackFixture.TimebasedInputBucketName}/{assetId}")),
                "pipeline-id-1234",
                A<List<CreateJobOutput>>.That.Matches(o => o.Single().Key.EndsWith(outputKey)),
                A<string>._,
                A<CancellationToken>._))
            .MustHaveHappened();

        A.CallTo(() => ElasticTranscoderWrapper.PersistJobId(assetId, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
