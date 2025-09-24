using System.Net;
using System.Text;
using System.Text.Json;
using DLCS.AWS.S3;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
using DLCS.Repository;
using DLCS.Repository.Strategy;
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
/// Tests for asset ingestion
/// </summary>
[Trait("Category", "Integration")]
[Collection(EngineCollection.CollectionName)]
public class ImageIngestTests : IClassFixture<ProtagonistAppFactory<Startup>>
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

    private readonly List<ImageDeliveryChannel> imageDeliveryChannels =
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
        },

        new()
        {
            Channel = AssetDeliveryChannels.Thumbnails,
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault,
            DeliveryChannelPolicy = new DeliveryChannelPolicy
            {
                Name = "default",
                PolicyData = "[\"!1024,1024\",\"!400,400\",\"!200,200\",\"!100,100\"]",
                Channel = AssetDeliveryChannels.Thumbnails
            }
        }
    ];

    public ImageIngestTests(ProtagonistAppFactory<Startup> appFactory, EngineFixture engineFixture)
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
        
        // Fake http image
        apiStub.Get("/image", (request, args) => "anything")
            .Header("Content-Type", "image/jpeg");

        engineFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task IngestAsset_Success_HttpOrigin_AllOpen()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/image";
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            mediaType: "image/tiff", width: 0, height: 0, imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow, null);

        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.Should().BeSuccessful();
        
        // S3 assets created
        BucketWriter.ShouldHaveKey(assetId.ToString()).ForBucket(LocalStackFixture.StorageBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/200.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/400.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/800.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/s.json").ForBucket(LocalStackFixture.ThumbsBucketName);
        
        // Database records updated
        var updatedAsset = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedAsset.Width.Should().Be(500);
        updatedAsset.Height.Should().Be(1000);
        updatedAsset.Ingesting.Should().BeFalse();
        updatedAsset.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedAsset.MediaType.Should().Be("image/tiff");
        updatedAsset.Error.Should().BeEmpty();

        var location = await dbContext.ImageLocations.SingleAsync(a => a.Id == assetId);
        location.Nas.Should().BeEmpty();
        location.S3.Should().Be($"s3://us-east-1/{LocalStackFixture.StorageBucketName}/{assetId}");

        var storage = await dbContext.ImageStorages.SingleAsync(a => a.Id == assetId);
        storage.Size.Should().BeGreaterThan(0);
        
        var policyData = await dbContext.AssetApplicationMetadata.SingleAsync(a => a.AssetId == assetId);
        policyData.MetadataValue.Should().Be("{\"a\": [], \"o\": [[400, 800], [200, 400], [100, 200]]}");
    }
    
    [Fact]
    public async Task IngestAsset_Success_HttpOrigin_AllOpen_InBatch()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/image";
        const int batchId = 39;
        var batch = await dbContext.Batches.AddTestBatch(batchId);
        batch.Entity.AddBatchAsset(assetId);
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            mediaType: "image/tiff", width: 0, height: 0, imageDeliveryChannels: imageDeliveryChannels, batch: batchId);
        
        var asset = entity.Entity;
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow, 39);

        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.Should().BeSuccessful();
        
        // S3 assets created
        BucketWriter.ShouldHaveKey(assetId.ToString()).ForBucket(LocalStackFixture.StorageBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/200.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/400.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/800.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/s.json").ForBucket(LocalStackFixture.ThumbsBucketName);
        
        // Database records updated
        var updatedAsset = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedAsset.Width.Should().Be(500);
        updatedAsset.Height.Should().Be(1000);
        updatedAsset.Ingesting.Should().BeFalse();
        updatedAsset.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedAsset.MediaType.Should().Be("image/tiff");
        updatedAsset.Error.Should().BeEmpty();

        var updatedBatch = await dbContext.Batches.Include(b => b.BatchAssets).SingleAsync(b => b.Id == batchId);
        updatedBatch.BatchAssets.Should()
            .ContainSingle(b => b.Status == BatchAssetStatus.Completed);

        var location = await dbContext.ImageLocations.SingleAsync(a => a.Id == assetId);
        location.Nas.Should().BeEmpty();
        location.S3.Should().Be($"s3://us-east-1/{LocalStackFixture.StorageBucketName}/{assetId}");

        var storage = await dbContext.ImageStorages.SingleAsync(a => a.Id == assetId);
        storage.Size.Should().BeGreaterThan(0);
        
        var policyData = await dbContext.AssetApplicationMetadata.SingleAsync(a => a.AssetId == assetId);
        policyData.MetadataValue.Should().Be("{\"a\": [], \"o\": [[400, 800], [200, 400], [100, 200]]}");
    }

    [Fact]
    public async Task IngestAsset_Success_UpdatesStorageOnReingest()
    {
        // Test that an existing storage value is overwritten with latest size
        const long sizeBeforeReingest = 950;
        
        var assetId = AssetIdGenerator.GetAssetId(CustomerForLimits, SpaceWithinLimit);

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/image";

        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            mediaType: "image/tiff", width: 0, height: 0, imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.Customers.AddTestCustomer(CustomerForLimits);
        await dbContext.Spaces.AddTestSpace(CustomerForLimits, SpaceWithinLimit);
        await dbContext.ImageStorages.AddTestImageStorage(id: assetId, space: SpaceWithinLimit,
            customer: CustomerForLimits, size: sizeBeforeReingest);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: CustomerForLimits, sizeOfStored: sizeBeforeReingest,
            storagePolicy: "medium");
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow, null);

        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        // Database records updated
        var storage = await dbContext.ImageStorages.SingleAsync(a => a.Id == assetId);
        storage.Size.Should().NotBe(sizeBeforeReingest, "Size has changed");
    }

    [Fact]
    public async Task IngestAsset_Success_ChangesMediaTypeToOriginContentType_WhenCalledWithUnknownImageType()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/image";
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            mediaType: "image/unknown", width: 0, height: 0, imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        asset.ImageDeliveryChannels = imageDeliveryChannels;
        await dbContext.SaveChangesAsync();
        
        var message = new IngestAssetRequest(entity.Entity.Id, DateTime.UtcNow, null);

        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.Should().BeSuccessful();
        
        // S3 assets created
        BucketWriter.ShouldHaveKey(assetId.ToString()).ForBucket(LocalStackFixture.StorageBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/200.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/400.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/800.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/s.json").ForBucket(LocalStackFixture.ThumbsBucketName);
        
        // Database records updated
        var updatedAsset = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedAsset.Width.Should().Be(500);
        updatedAsset.Height.Should().Be(1000);
        updatedAsset.Ingesting.Should().BeFalse();
        updatedAsset.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedAsset.MediaType.Should().Be("image/jpeg", "MediaType change to origin contentType");
        updatedAsset.Error.Should().BeEmpty();
    }
    
    [Fact]
    public async Task IngestAsset_Error_ExceedAllowance()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(CustomerForLimits, SpaceExceedLimit);

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/image";

        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin, customer: CustomerForLimits,
            width: 0, height: 0, mediaType: "image/tiff", imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.Customers.AddTestCustomer(CustomerForLimits);
        await dbContext.Spaces.AddTestSpace(CustomerForLimits, SpaceExceedLimit);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: CustomerForLimits, sizeOfStored: 99,
            storagePolicy: "small");
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow, null);

        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InsufficientStorage);

        // No S3 assets created
        BucketWriter.ShouldNotHaveKey(assetId.ToString());
        
        // Database records updated
        var updatedAsset = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedAsset.Width.Should().Be(0);
        updatedAsset.Height.Should().Be(0);
        updatedAsset.Ingesting.Should().BeFalse();
        updatedAsset.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(SpaceExceedLimit));
        updatedAsset.Error.Should().Be("StoragePolicy size limit exceeded");

        var location = await dbContext.ImageLocations.SingleOrDefaultAsync(a => a.Id == assetId);
        location.Should().BeNull();

        var storage = await dbContext.ImageStorages.SingleOrDefaultAsync(a => a.Id == assetId);
        storage.Should().BeNull();
    }
    
    [Fact]
    public async Task IngestAsset_Error_ExceedAllowanceOnReingest()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(CustomerForLimits, SpaceExceedLimitReingest);

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/image";

        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin, customer: CustomerForLimits,
            width: 0, height: 0, mediaType: "image/tiff", imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.Customers.AddTestCustomer(CustomerForLimits);
        await dbContext.Spaces.AddTestSpace(CustomerForLimits, SpaceExceedLimitReingest);
        await dbContext.ImageStorages.AddTestImageStorage(id: assetId, space: SpaceExceedLimitReingest,
            customer: CustomerForLimits, size: 500);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: CustomerForLimits, sizeOfStored: 950,
            storagePolicy: "medium");
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow, null);

        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InsufficientStorage);

        // No S3 assets created
        BucketWriter.ShouldNotHaveKey(assetId.ToString());
        
        // Database records updated
        var updatedAsset = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedAsset.Width.Should().Be(0);
        updatedAsset.Height.Should().Be(0);
        updatedAsset.Ingesting.Should().BeFalse();
        updatedAsset.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedAsset.Error.Should().Be("StoragePolicy size limit exceeded");

        var location = await dbContext.ImageLocations.SingleOrDefaultAsync(a => a.Id == assetId);
        location.Should().BeNull();

        var storage = await dbContext.ImageStorages.SingleOrDefaultAsync(a => a.Id == assetId);
        storage!.Size.Should().Be(500);
    }
    
    [Fact]
    public async Task IngestAsset_Error_HttpOrigin()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/this-will-fail";
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            imageOptimisationPolicy: "fast-higher", mediaType: "image/tiff", width: 0, height: 0, duration: 0,
            imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow, null);

        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.Should().HaveServerError();

        // No S3 assets created
        BucketWriter.ShouldNotHaveKey(assetId.ToString());
        
        // Database records updated
        var updatedAsset = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedAsset.Width.Should().Be(0);
        updatedAsset.Height.Should().Be(0);
        updatedAsset.Ingesting.Should().BeFalse();
        updatedAsset.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedAsset.Error.Should().NotBeEmpty();

        var location = await dbContext.ImageLocations.SingleOrDefaultAsync(a => a.Id == assetId);
        location.Should().BeNull();

        var storage = await dbContext.ImageStorages.SingleOrDefaultAsync(a => a.Id == assetId);
        storage.Should().BeNull();
    }
}

public class FakeFileSaver : IFileSaver
{
    private readonly List<string> createdFiles = new();
    private readonly List<AssetId> savedAssets = new();

    public Task<long> SaveResponseToDisk(AssetId assetId, OriginResponse originResponse, string destination,
        CancellationToken cancellationToken = default)
    {
        createdFiles.Add(destination);
        savedAssets.Add(assetId);
        return Task.FromResult(1000L);
    }
}
