using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.MediaConvert.Model;
using DLCS.AWS.MediaConvert.Models;
using DLCS.AWS.S3;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;
using DLCS.AWS.Transcoding.Models.Request;
using DLCS.Core.FileSystem;
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
using CreateJobResponse = DLCS.AWS.Transcoding.Models.Request.CreateJobResponse;

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
    private static readonly ITranscoderWrapper TranscoderWrapper = A.Fake<ITranscoderWrapper>();
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
                    .AddSingleton<ITranscoderWrapper>(TranscoderWrapper);
            })
            .WithConnectionString(engineFixture.DbFixture.ConnectionString)
            .CreateClient();
        
        // Fake http origins
        apiStub.Get("/video", (request, args) => "anything")
            .Header("Content-Type", "video/mpeg");
        apiStub.Get("/audio", (request, args) => "anything")
            .Header("Content-Type", "audio/mpeg");

        engineFixture.DbFixture.CleanUp();

        A.CallTo(() => TranscoderWrapper.GetPipelineId("protagonist-pipeline", A<CancellationToken>._))
            .Returns("pipeline-id-1234");
    }
    
    // This relise on settings in appSettings.Testing.json
    private static string GetPresetName(string type) => type == "audio" ? "Custom-audio_mp3_128k" : "System-Generic_Hd";

    [Theory]
    [InlineData("video", 6)]
    [InlineData("audio", 5)]
    public async Task IngestAsset_CreatesTranscoderJob_HttpOrigin(string type, int policyId)
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

        A.CallTo(() => TranscoderWrapper.CreateJob(
                A<string>._,
                A<string>._,
                A<MediaConvertJobGroup>._,
                A<Dictionary<string, string>>._,
                A<CancellationToken>._))
            .Returns(new CreateJobResponse(jobId, HttpStatusCode.Accepted));
        
        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.Should().BeSuccessful();
        
        // S3 assets created for transcoding
        BucketWriter
            .ShouldHaveKeyThatStartsWith(assetId.ToString())
            .WithContentType($"{type}/mpeg")
            .ForBucket(LocalStackFixture.TimebasedInputBucketName);
        
        // MC job created
        A.CallTo(() => TranscoderWrapper.CreateJob(
                A<string>.That.Matches(s =>
                    s.StartsWith($"s3://{LocalStackFixture.TimebasedInputBucketName}/{assetId}")),
                "pipeline-id-1234",
                A<MediaConvertJobGroup>.That.Matches(o => o.Outputs.Single().Preset == GetPresetName(type)),
                A<Dictionary<string, string>>.That.Matches(d =>
                    d[TranscodeMetadataKeys.DlcsId] == assetId.ToString()
                    && d[TranscodeMetadataKeys.OriginSize] == "0"
                    && d[TranscodeMetadataKeys.BatchId] == batch.ToString()
                    && d[TranscodeMetadataKeys.MediaType] == $"{type}/mpeg"
                ),
                A<CancellationToken>._))
            .MustHaveHappened();

        // Metadata persisted
        A.CallTo(() => TranscoderWrapper.PersistJobId(assetId, jobId, A<CancellationToken>._))
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
    [InlineData("video", 6)]
    [InlineData("audio", 5)]
    public async Task IngestAsset_ReturnsNoSuccess_IfCreateTranscoderJobFails(string type, int policyId)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(assetPostfix: type);
        const string jobId = "1234567890123-abcdef";

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

        A.CallTo(() => TranscoderWrapper.CreateJob(
                A<string>._,
                A<string>._,
                A<MediaConvertJobGroup>._,
                A<Dictionary<string, string>>._,
                A<CancellationToken>._))
            .Returns(new CreateJobResponse(jobId, HttpStatusCode.BadRequest));

        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        // S3 assets created for transcoding + a metadata file
        BucketWriter
            .ShouldHaveKeyThatStartsWith(assetId.ToString())
            .WithContentType($"{type}/mpeg")
            .ForBucket(LocalStackFixture.TimebasedInputBucketName);

        // MC job created
        A.CallTo(() => TranscoderWrapper.CreateJob(
                A<string>.That.Matches(s => s.StartsWith($"s3://{LocalStackFixture.TimebasedInputBucketName}/{assetId}")),
                "pipeline-id-1234",
                A<MediaConvertJobGroup>.That.Matches(o => o.Outputs.Single().Preset == GetPresetName(type)),
                A<Dictionary<string, string>>.That.Matches(d =>
                    d[TranscodeMetadataKeys.DlcsId] == assetId.ToString() && d[TranscodeMetadataKeys.BatchId] == string.Empty),
                A<CancellationToken>._))
            .MustHaveHappened();

        A.CallTo(() => TranscoderWrapper.PersistJobId(assetId, A<string>._, A<CancellationToken>._))
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

        var imageDeliveryChannels = new List<ImageDeliveryChannel>
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

        A.CallTo(() => TranscoderWrapper.CreateJob(
                A<string>._,
                A<string>._,
                A<MediaConvertJobGroup>._,
                A<Dictionary<string, string>>._,
                A<CancellationToken>._))
            .Returns(new CreateJobResponse(jobId, HttpStatusCode.Accepted));

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
            .WithContentType($"{type}/mpeg")
            .ForBucket(LocalStackFixture.StorageBucketName);
        BucketWriter
            .ShouldHaveKeyThatStartsWith(assetId.ToString(), true)
            .WithContentType($"{type}/mpeg")
            .ForBucket(LocalStackFixture.TimebasedInputBucketName);

        // MC job created
        A.CallTo(() => TranscoderWrapper.CreateJob(
                A<string>.That.Matches(
                    s => s.StartsWith($"s3://{LocalStackFixture.TimebasedInputBucketName}/{assetId}")),
                "pipeline-id-1234",
                A<MediaConvertJobGroup>.That.Matches(o => o.Outputs.Single().Preset == GetPresetName(type)),
                A<Dictionary<string, string>>.That.Matches(d =>
                    d[TranscodeMetadataKeys.DlcsId] == assetId.ToString()
                    && d[TranscodeMetadataKeys.OriginSize] == "1000"
                    && d[TranscodeMetadataKeys.BatchId] == string.Empty
                    && d[TranscodeMetadataKeys.MediaType] == $"{type}/mpeg"
                    ),
                A<CancellationToken>._))
            .MustHaveHappened();

        A.CallTo(() => TranscoderWrapper.PersistJobId(assetId, jobId, A<CancellationToken>._))
            .MustHaveHappened();
    }
}
