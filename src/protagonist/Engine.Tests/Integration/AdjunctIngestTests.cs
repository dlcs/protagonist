using System.Text;
using System.Text.Json;
using DLCS.AWS.S3;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
using DLCS.Repository;
using DLCS.Repository.Strategy.Utils;
using Engine.Ingest.Image;
using Engine.Ingest.Image.ImageServer.Models;
using Engine.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stubbery;
using Test.Helpers;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using Test.Helpers.Storage;

namespace Engine.Tests.Integration;

/// <summary>
/// Tests for adjunct ingestion
/// </summary>
[Trait("Category", "Integration")]
[Collection(EngineCollection.CollectionName)]
public class AdjunctIngestTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);
    private readonly DlcsContext dbContext;
    private static readonly TestBucketWriter BucketWriter = new();
    private readonly ApiStub apiStub;
    
    // These spaces are used in tests 
    private const int CustomerForLimits = -10;
    private const int SpaceExceedLimit = 1;
    private const int SpaceWithinLimit = 2;
    private const int SpaceExceedLimitReingest = 3;
    
    public AdjunctIngestTests(ProtagonistAppFactory<Startup> appFactory, EngineFixture engineFixture)
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
                    .AddSingleton<IBucketWriter>(BucketWriter);
            })
            .WithConfigValue("OrchestratorBaseUrl", apiStub.Address)
            .WithConfigValue("ImageIngest:ImageProcessorUrl", apiStub.Address)
            .WithConnectionString(engineFixture.DbFixture.ConnectionString)
            .CreateClient();
        
        // Stubbed appetiser
        var appetiserResponse = new AppetiserResponseModel
        {
            Height = 1000, Width = 500, Thumbs =
            [
                new ImageOnDisk { Height = 800, Width = 400, Path = "/path/to/800.jpg" },
                new ImageOnDisk { Height = 400, Width = 200, Path = "/path/to/400.jpg" },
                new ImageOnDisk { Height = 200, Width = 100, Path = "/path/to/200.jpg" }
            ],
            JP2 = "/path/to.jp2"
        };

        var appetiserResponseJson = JsonSerializer.Serialize(appetiserResponse, settings);
        apiStub.Post("/convert", (request, args) => appetiserResponseJson)
            .Header("Content-Type", "application/json");
        
        // Fake http images
        apiStub.Get("/image", (request, args) => "anything")
            .Header("Content-Type", "image/jpeg");
        
        apiStub.Get("/image/adjunct", (request, args) => "anything")
            .Header("Content-Type", "image/jpeg");

        engineFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task IngestAdjunct_Success()
    {
        var asset = await CreateParentAsset();

        const string adjunctId = nameof(IngestAdjunct_Success);
        
        // Note - API would have set this up before handing of
        var origin = $"{apiStub.Address}/image/adjunct";
        var adjunct = new Adjunct
        {
            Id = adjunctId, AssetId = asset.Id, Asset = asset, IIIFLink = IIIFLinkType.SeeAlso,
            MediaType = "image/jpeg", Type = "Image", Origin = origin, Created = DateTime.UtcNow, Error = string.Empty, Ingesting = true
        };
        
        dbContext.Adjuncts.Add(adjunct);
        
        await dbContext.SaveChangesAsync();
        
        var message = new IngestAdjunctRequest(adjunct.Id, adjunct.AssetId, DateTime.UtcNow);
        
        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        
        var result = await httpClient.PostAsync("adjunct-ingest", jsonContent);
        
        // Assert
        result.Should().BeSuccessful();
        
        BucketWriter.ShouldHaveKey($"{asset.Id}/adjuncts/{adjunct.Id}").ForBucket(LocalStackFixture.StorageBucketName);
        
        var updatedAdjunct =  await dbContext.Adjuncts.SingleAsync(a => a.Id == adjunctId && a.AssetId == asset.Id);
        updatedAdjunct.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedAdjunct.Ingesting.Should().BeFalse();
        updatedAdjunct.Error.Should().BeEmpty();
        updatedAdjunct.Size.Should().BeGreaterThan(0);
        
        var storage = await dbContext.ImageStorages.SingleAsync(a => a.Id == asset.Id);
        storage.AdjunctSize.Should().BeGreaterThan(0);
    }

    private async Task<Asset> CreateParentAsset()
    {
        List<ImageDeliveryChannel> imageDeliveryChannels =
        [
            new()
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault,
                DeliveryChannelPolicy = new DeliveryChannelPolicy
                {
                    Name = "default",
                    Channel = AssetDeliveryChannels.Image
                }
            }
        ];
        
        var assetId = AssetIdGenerator.GetAssetId();

        // Note - API would have set this up before handing off
        var origin = $"{apiStub.Address}/image";
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            mediaType: "image/tiff", width: 0, height: 0, imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.SaveChangesAsync();
        
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow, null);
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);
        
        // this isn't a test per-se, but we want to stop if that step failed
        result.Should().BeSuccessful();

        return asset;
    }
}
