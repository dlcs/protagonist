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
using Engine.Ingest.Image.ImageServer.Measuring;
using Engine.Ingest.Image.ImageServer.Models;
using Engine.Tests.Integration.Infrastructure;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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
public class ImageIngestTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);
    private readonly DlcsContext dbContext;
    private static readonly TestBucketWriter BucketWriter = new();
    private readonly ApiStub apiStub;
    private readonly IImageMeasurer imageMeasurer;
    
    private readonly List<ImageDeliveryChannel> imageDeliveryChannels = new()
    {
        new ImageDeliveryChannel
        {
            Channel = AssetDeliveryChannels.Image,
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Name = "default",
                Channel = AssetDeliveryChannels.Image
            }
        },
        new ImageDeliveryChannel
        {
            Channel = AssetDeliveryChannels.Thumbnails,
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Name = "default",
                PolicyData = "[\"!1024,1024\",\"!400,400\",\"!200,200\",\"!100,100\"]",
                Channel = AssetDeliveryChannels.Thumbnails
            }
        }
    };

    public ImageIngestTests(ProtagonistAppFactory<Startup> appFactory, EngineFixture engineFixture)
    {
        dbContext = engineFixture.DbFixture.DbContext;
        apiStub = engineFixture.ApiStub;
        imageMeasurer = A.Fake<IImageMeasurer>();
        httpClient = appFactory
            .WithTestServices(services =>
            {
                // Mock out things that write to disk or read from disk
                services
                    .AddSingleton<IFileSaver, FakeFileSaver>()
                    .AddSingleton<IFileSystem, FakeFileSystem>()
                    .AddSingleton(imageMeasurer)
                    .AddSingleton<IBucketWriter>(BucketWriter);
            })
            .WithConfigValue("OrchestratorBaseUrl", apiStub.Address)
            .WithConfigValue("ImageIngest:ImageProcessorUrl", apiStub.Address)
            .WithConfigValue("ImageIngest:ThumbsProcessorUrl", apiStub.Address)
            .WithConnectionString(engineFixture.DbFixture.ConnectionString)
            .CreateClient();

        // Stubbed appetiser
        var appetiserResponse = new AppetiserResponseModel
        {
            Height = 1024, Width = 1024
        };

        A.CallTo(() => imageMeasurer.MeasureImage(A<string>.That.EndsWith("thumb1"), A<CancellationToken>._))
            .Returns(Task.FromResult(new ImageOnDisk { Width = 1024, Height = 1024 }));
        A.CallTo(() => imageMeasurer.MeasureImage(A<string>.That.EndsWith("thumb2"), A<CancellationToken>._))
            .Returns(Task.FromResult(new ImageOnDisk { Width = 400, Height = 400 }));
        A.CallTo(() => imageMeasurer.MeasureImage(A<string>.That.EndsWith("thumb3"), A<CancellationToken>._))
            .Returns(Task.FromResult(new ImageOnDisk { Width = 200, Height = 200 }));
        A.CallTo(() => imageMeasurer.MeasureImage(A<string>.That.EndsWith("thumb4"), A<CancellationToken>._))
            .Returns(Task.FromResult(new ImageOnDisk { Width = 100, Height = 100 }));
        
        var testImage = GenerateTestImageByteData();
        
        var appetiserResponseJson = JsonSerializer.Serialize(appetiserResponse, settings);
        apiStub.Post("/convert", (request, args) => appetiserResponseJson)
            .Header("Content-Type", "application/json");
        
        apiStub.Get("iiif/3/{arg1}/full/{arg2}/0/default.jpg", (request, args) => testImage);
        
        // Fake http image
        apiStub.Get("/image", (request, args) => "anything")
            .Header("Content-Type", "image/jpeg");

        engineFixture.DbFixture.CleanUp();
    }
    
    private byte[] GenerateTestImageByteData()
    {
        using var image = new Image<Rgba32>(1024, 1024);

        //draw a useless line for some data
        image.Mutate(imageContext =>
        {
            // draw background
            var bgColor = Rgba32.ParseHex("#f00a21");
            imageContext.BackgroundColor(bgColor);
        });
        
        //Convert to byte array
        MemoryStream memoryStream = new MemoryStream();
        byte[] jpegData;

        using (memoryStream)
        {
            image.SaveAsJpeg(memoryStream);
            jpegData = memoryStream.ToArray();
        }
        return jpegData;
    }

    [Fact]
    public async Task IngestAsset_Success_HttpOrigin_AllOpen()
    {
        // Arrange
        var assetId = AssetId.FromString( $"99/1/{nameof(IngestAsset_Success_HttpOrigin_AllOpen)}");

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/image";
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            imageOptimisationPolicy: "fast-higher", mediaType: "image/tiff", width: 0, height: 0, duration: 0,
            imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow);

        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.Should().BeSuccessful();
        
        // S3 assets created
        BucketWriter.ShouldHaveKey(assetId.ToString()).ForBucket(LocalStackFixture.StorageBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/low.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/200.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/400.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/1024.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/s.json").ForBucket(LocalStackFixture.ThumbsBucketName);
        
        // Database records updated
        var updatedAsset = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedAsset.Width.Should().Be(1024);
        updatedAsset.Height.Should().Be(1024);
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
        policyData.MetadataValue.Should().Be("{\"a\": [], \"o\": [[1024, 1024], [400, 400], [200, 200], [100, 100]]}");
    }
    
     [Fact]
    public async Task IngestAsset_Success_OnLargerReingest()
    {
        // Arrange
        // Create a new customer to have control over CustomerStorage and make sure it's isolated
        const int customerId = -10;
        var assetId = AssetId.FromString($"{customerId}/2/{nameof(IngestAsset_Success_OnLargerReingest)}");

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/image";

        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin, 
            mediaType: "image/tiff", width: 0, height: 0, duration: 0,
            imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.Customers.AddTestCustomer(customerId);
        await dbContext.Spaces.AddTestSpace(customerId, 2);
        await dbContext.ImageStorages.AddTestImageStorage(id: assetId, space: 2, customer: customerId, size: 950);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: customerId, sizeOfStored: 950,
            storagePolicy: "medium");
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow);

        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        // Database records updated
        var updatedAsset = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedAsset.Width.Should().Be(1024);
        updatedAsset.Height.Should().Be(1024);
        updatedAsset.Ingesting.Should().BeFalse();
        updatedAsset.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedAsset.MediaType.Should().Be("image/tiff");
        updatedAsset.Error.Should().BeEmpty();

        var location = await dbContext.ImageLocations.SingleAsync(a => a.Id == assetId);
        location.Nas.Should().BeEmpty();
        location.S3.Should().Be($"s3://us-east-1/{LocalStackFixture.StorageBucketName}/{assetId}");

        var storage = await dbContext.ImageStorages.SingleAsync(a => a.Id == assetId);
        storage.Size.Should().NotBe(950);
        
        var policyData = await dbContext.AssetApplicationMetadata.SingleAsync(a => a.AssetId == assetId);
        policyData.MetadataValue.Should().Be("{\"a\": [], \"o\": [[1024, 1024], [400, 400], [200, 200], [100, 100]]}");
    }

    [Fact]
    public async Task IngestAsset_Success_ChangesMediaTypeToContentType_WhenCalledWithUnknownImageType()
    {
        // Arrange
        var assetId = AssetId.FromString($"99/1/{nameof(IngestAsset_Success_ChangesMediaTypeToContentType_WhenCalledWithUnknownImageType)}");

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/image";
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin, 
            mediaType: "image/unknown", width: 0, height: 0, duration: 0,
            imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        asset.ImageDeliveryChannels = imageDeliveryChannels;
        await dbContext.SaveChangesAsync();
        
        var message = new IngestAssetRequest(entity.Entity.Id, DateTime.UtcNow);

        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        var result = await httpClient.PostAsync("asset-ingest", jsonContent);

        // Assert
        result.Should().BeSuccessful();
        
        // S3 assets created
        BucketWriter.ShouldHaveKey(assetId.ToString()).ForBucket(LocalStackFixture.StorageBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/low.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/200.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/400.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/open/1024.jpg").ForBucket(LocalStackFixture.ThumbsBucketName);
        BucketWriter.ShouldHaveKey($"{assetId}/s.json").ForBucket(LocalStackFixture.ThumbsBucketName);
        
        // Database records updated
        var updatedAsset = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedAsset.Width.Should().Be(1024);
        updatedAsset.Height.Should().Be(1024);
        updatedAsset.Ingesting.Should().BeFalse();
        updatedAsset.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedAsset.MediaType.Should().Be("image/jpeg");
        updatedAsset.Error.Should().BeEmpty();

        var location = await dbContext.ImageLocations.SingleAsync(a => a.Id == assetId);
        location.Nas.Should().BeEmpty();
        location.S3.Should().Be($"s3://us-east-1/{LocalStackFixture.StorageBucketName}/{assetId}");

        var storage = await dbContext.ImageStorages.SingleAsync(a => a.Id == assetId);
        storage.Size.Should().BeGreaterThan(0);
        
        var policyData = await dbContext.AssetApplicationMetadata.SingleAsync(a => a.AssetId == assetId);
        policyData.MetadataValue.Should().Be("{\"a\": [], \"o\": [[1024, 1024], [400, 400], [200, 200], [100, 100]]}");
    }
    
    [Fact]
    public async Task IngestAsset_Error_ExceedAllowance()
    {
        // Arrange
        // Create a new customer to have control over CustomerStorage and make sure it's isolated
        const int customerId = -10;
        var assetId = AssetId.FromString($"{customerId}/1/{nameof(IngestAsset_Error_ExceedAllowance)}");

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/image";

        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin, customer: customerId,
            width: 0, height: 0, duration: 0, mediaType: "image/tiff", imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.Customers.AddTestCustomer(customerId);
        await dbContext.Spaces.AddTestSpace(customerId, 1);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: customerId, sizeOfStored: 99,
            storagePolicy: "small");
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow);

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
        storage.Should().BeNull();
    }
    
    [Fact]
    public async Task IngestAsset_Error_ExceedAllowanceOnReingest()
    {
        // Arrange
        // Create a new customer to have control over CustomerStorage and make sure it's isolated
        const int customerId = -10;
        var assetId = AssetId.FromString($"{customerId}/3/{nameof(IngestAsset_Error_ExceedAllowanceOnReingest)}");

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/image";

        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin, customer: customerId,
            width: 0, height: 0, duration: 0, mediaType: "image/tiff", imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.Customers.AddTestCustomer(customerId);
        await dbContext.Spaces.AddTestSpace(customerId, 3);
        await dbContext.ImageStorages.AddTestImageStorage(id: assetId, space: 2, customer: customerId, size: 500);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: customerId, sizeOfStored: 950,
            storagePolicy: "medium");
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow);

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
        var assetId = AssetId.FromString($"99/1/{nameof(IngestAsset_Error_HttpOrigin)}");

        // Note - API will have set this up before handing off
        var origin = $"{apiStub.Address}/this-will-fail";
        var entity = await dbContext.Images.AddTestAsset(assetId, ingesting: true, origin: origin,
            imageOptimisationPolicy: "fast-higher", mediaType: "image/tiff", width: 0, height: 0, duration: 0,
            imageDeliveryChannels: imageDeliveryChannels);
        var asset = entity.Entity;
        await dbContext.SaveChangesAsync();
        var message = new IngestAssetRequest(asset.Id, DateTime.UtcNow);

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
    public List<string> CreatedFiles = new();
    public List<AssetId> SavedAssets = new();

    public Task<long> SaveResponseToDisk(AssetId assetId, OriginResponse originResponse, string destination,
        CancellationToken cancellationToken = default)
    {
        CreatedFiles.Add(destination);
        SavedAssets.Add(assetId);
        return Task.FromResult(1000L);
    }
}