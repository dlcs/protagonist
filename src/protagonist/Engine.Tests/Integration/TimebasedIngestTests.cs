using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.AWS.S3;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
using DLCS.Repository;
using DLCS.Repository.Strategy.Utils;
using Engine.Tests.Integration.Infrastructure;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stubbery;
using Test.Helpers;
using Test.Helpers.Data;
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
    private static readonly IElasticTranscoderPresetLookup ElasticTranscoderPreset = A.Fake<IElasticTranscoderPresetLookup>();
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
                    .AddSingleton<IElasticTranscoderWrapper>(ElasticTranscoderWrapper)
                    .AddSingleton<IElasticTranscoderPresetLookup>(ElasticTranscoderPreset);
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
        A.CallTo(() => ElasticTranscoderPreset.GetPresetLookupByName(A<CancellationToken>._))
            .Returns(new Dictionary<string, TranscoderPreset>
            {
                ["System preset: Generic 720p"] = new ("123-123", "System preset: Generic 720p", ""),
                ["System preset: Audio MP3 - 128k"] = new ("456-456", "System preset: Audio MP3 - 128k", "")
            });
    }

    [Theory]
    [InlineData("video", "/full/full/max/max/0/default.mp4", 6)]
    [InlineData("audio", "/full/max/default.mp3", 5)]
    public async Task IngestAsset_CreatesTranscoderJob_HttpOrigin(string type, string expectedKey, int policyId)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(assetPostfix: type);
        const string jobId = "1234567890123-abcdef";
        
        var origin = $"{apiStub.Address}/{type}";
        const int batch = 999;
        var dbBatch = await dbContext.Batches.AddTestBatch(batch);
        dbBatch.Entity.AddBatchAsset(assetId);
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            mediaType: $"{type}/mpeg", family: AssetFamily.Timebased,
            imageDeliveryChannels: new List<ImageDeliveryChannel>
            {
                new()
                {
                    Channel = AssetDeliveryChannels.Timebased,
                    DeliveryChannelPolicyId = policyId
                }
            }, batch: batch);
        var asset = entity.Entity;
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow, batch);

        A.CallTo(() => ElasticTranscoderWrapper.CreateJob(
                A<string>._,
                A<string>._,
                A<List<CreateJobOutput>>._,
                A<Dictionary<string, string>>._,
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
                A<string>.That.Matches(
                    s => s.StartsWith($"s3://{LocalStackFixture.TimebasedInputBucketName}/{assetId}")),
                "pipeline-id-1234",
                A<List<CreateJobOutput>>.That.Matches(o => o.Single().Key.EndsWith(outputKey)),
                A<Dictionary<string, string>>.That.Matches(d => 
                    d[UserMetadataKeys.DlcsId] == assetId.ToString() 
                    && d[UserMetadataKeys.OriginSize] == "0"
                    && d[UserMetadataKeys.BatchId] == batch.ToString()
                    ),
                A<CancellationToken>._))
            .MustHaveHappened();

        A.CallTo(() => ElasticTranscoderWrapper.PersistJobId(assetId, jobId, A<CancellationToken>._))
            .MustHaveHappened();

        var savedBatch = await dbContext.Batches
            .Include(b => b.BatchAssets)
            .SingleAsync(b => b.Id == batch);
        savedBatch.Finished.Should().BeNull();
        savedBatch.BatchAssets.Should()
            .ContainSingle(
                ba => ba.AssetId == assetId && !ba.Finished.HasValue && ba.Status == BatchAssetStatus.Waiting,
                "BatchAsset is not marked as complete");
    }
    
    [Theory]
    [InlineData("video", "/full/full/max/max/0/default.mp4", 6)]
    [InlineData("audio", "/full/max/default.mp3", 5)]
    public async Task IngestAsset_ReturnsNoSuccess_IfCreateTranscoderJobFails(string type, string expectedKey, int policyId)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(assetPostfix: type);
        
        var origin = $"{apiStub.Address}/{type}";
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            mediaType: $"{type}/mpeg", family: AssetFamily.Timebased,
            imageDeliveryChannels: new List<ImageDeliveryChannel>
            {
                new ()
                {
                    Channel = AssetDeliveryChannels.Timebased,
                    DeliveryChannelPolicyId = policyId
                }
            });
        var asset = entity.Entity;
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow, null);

        A.CallTo(() => ElasticTranscoderWrapper.CreateJob(
                A<string>._,
                A<string>._,
                A<List<CreateJobOutput>>._,
                A<Dictionary<string, string>>._,
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
                A<string>.That.Matches(s => s.StartsWith($"s3://{LocalStackFixture.TimebasedInputBucketName}/{assetId}")),
                "pipeline-id-1234",
                A<List<CreateJobOutput>>.That.Matches(o => o.Single().Key.EndsWith(outputKey)),
                A<Dictionary<string, string>>.That.Matches(d => 
                    d[UserMetadataKeys.DlcsId] == assetId.ToString() && d[UserMetadataKeys.BatchId] == string.Empty),
                A<CancellationToken>._))
            .MustHaveHappened();

        A.CallTo(() => ElasticTranscoderWrapper.PersistJobId(assetId, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("video", "/full/full/max/max/0/default.mp4", 6)]
    [InlineData("audio", "/full/max/default.mp3", 5)]
    public async Task IngestAsset_SetsFileSizeCorrectly_IfAlsoAvailableForFileChannel(string type, string expectedKey, int deliveryChannelPolicyId)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(assetPostfix: type);
        const string jobId = "1234567890123-abcdef";

        var imageDeliveryChannels = new List<ImageDeliveryChannel>()
        {
            new()
            {
                Channel = AssetDeliveryChannels.Timebased,
                DeliveryChannelPolicyId = deliveryChannelPolicyId
            },
            new()
            {
                Channel = AssetDeliveryChannels.File,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
            }
        };
        
        var origin = $"{apiStub.Address}/{type}";
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            mediaType: $"{type}/mpeg", family: AssetFamily.Timebased,
            imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow, null);

        A.CallTo(() => ElasticTranscoderWrapper.CreateJob(
                A<string>._,
                A<string>._,
                A<List<CreateJobOutput>>._,
                A<Dictionary<string, string>>._,
                A<CancellationToken>._))
            .Returns(new CreateJobResponse { HttpStatusCode = HttpStatusCode.Accepted, Job = new Job { Id = jobId } });
        
        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.Should().BeSuccessful();
        
        // S3 assets created for transcoding + origin uploaded due to "file" channel
        var outputKey = $"{assetId}{expectedKey}";
        BucketWriter
            .ShouldHaveKey($"{assetId}/original")
            .ForBucket(LocalStackFixture.StorageBucketName);
        BucketWriter
            .ShouldHaveKeyThatStartsWith(assetId.ToString(), true)
            .ForBucket(LocalStackFixture.TimebasedInputBucketName);
        
        // ET job created
        A.CallTo(() => ElasticTranscoderWrapper.CreateJob(
                A<string>.That.Matches(
                    s => s.StartsWith($"s3://{LocalStackFixture.TimebasedInputBucketName}/{assetId}")),
                "pipeline-id-1234",
                A<List<CreateJobOutput>>.That.Matches(o => o.Single().Key.EndsWith(outputKey)),
                A<Dictionary<string, string>>.That.Matches(d => 
                    d[UserMetadataKeys.DlcsId] == assetId.ToString() 
                    && d[UserMetadataKeys.OriginSize] == "1000"
                    && d[UserMetadataKeys.BatchId] == string.Empty
                    ),
                A<CancellationToken>._))
            .MustHaveHappened();

        A.CallTo(() => ElasticTranscoderWrapper.PersistJobId(assetId, jobId, A<CancellationToken>._))
            .MustHaveHappened();
    }
}
