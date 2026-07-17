using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Amazon.S3.Model;
using DLCS.AWS.S3;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
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
    private readonly LocalStackFixture localStack;
    
    // These spaces are used in tests 
    private const int CustomerForLimits = -20;
    private const int SpaceExceedLimit = 1;

    private string origin2k;
    private string origin4k;
    
    public AdjunctIngestTests(ProtagonistAppFactory<Startup> appFactory, EngineFixture engineFixture)
    {
        dbContext = engineFixture.DbFixture.DbContext;
        apiStub = engineFixture.ApiStub;
        localStack = engineFixture.LocalStackFixture;
        
        // Fake http images
        apiStub.Get("/image", (request, args) => "anything")
            .Header("Content-Type", "image/jpeg");
        
        apiStub.Get("/image/adjunct2k", (request, args) => "blob2kb")
            .Header("Content-Type", "image/jpeg");
        origin2k = $"{apiStub.Address}/image/adjunct2k";
        
        apiStub.Get("/image/adjunct4k", (request, args) => "blob4kb")
            .Header("Content-Type", "image/jpeg");
        origin4k = $"{apiStub.Address}/image/adjunct4k";

        var saver = new FakeAdjunctSaver()
            .WithFileSize(origin2k, 2048)
            .WithFileSize(origin4k, 4096);
        
        httpClient = appFactory
            .WithTestServices(services =>
            {
                // Mock out things that write to disk or read from disk
                services
                    .AddSingleton<IFileSaver>(saver)
                    .AddSingleton<IFileSystem, FakeFileSystem>()
                    .AddSingleton<IBucketWriter>(BucketWriter);
            })
            .WithConfigValue("OrchestratorBaseUrl", apiStub.Address)
            .WithConfigValue("ImageIngest:ImageProcessorUrl", apiStub.Address)
            .WithConnectionString(engineFixture.DbFixture.ConnectionString)
            .WithLocalStack(localStack)
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
        

        engineFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task IngestAdjunct_Success()
    {
        var asset = await CreateParentAsset();

        const string adjunctId = nameof(IngestAdjunct_Success);
        
        // Note - API would have set this up before handing of
        var adjunct = new Adjunct
        {
            Id = adjunctId, AssetId = asset.Id, Asset = asset, IIIFLink = IIIFLinkType.SeeAlso,
            MediaType = "image/jpeg", Type = "Image", Origin = origin2k, Created = DateTime.UtcNow, Error = string.Empty, Ingesting = true
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
        storage.AdjunctSize.Should().Be(2048);
    }
    
    [Fact]
    public async Task IngestAdjunct_Success_ReingestUpdatesAdjunctSize()
    {
        var asset = await CreateParentAsset();

        const string adjunctId = nameof(IngestAdjunct_Success_ReingestUpdatesAdjunctSize);
        
        // Note - API would have set this up before handing of
        var adjunct = new Adjunct
        {
            Id = adjunctId, AssetId = asset.Id, Asset = asset, IIIFLink = IIIFLinkType.SeeAlso,
            MediaType = "image/jpeg", Type = "Image", Origin = origin2k, Created = DateTime.UtcNow, Error = string.Empty, Ingesting = true
        };
        
        dbContext.Adjuncts.Add(adjunct);
        
        await dbContext.SaveChangesAsync();
        
        var message = new IngestAdjunctRequest(adjunct.Id, adjunct.AssetId, DateTime.UtcNow);
        
        // Act 1 - ingest adjunct
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        
        var result = await httpClient.PostAsync("adjunct-ingest", jsonContent);
        
        // Assert 1
        result.Should().BeSuccessful();
        
        var storage = await dbContext.ImageStorages.SingleAsync(a => a.Id == asset.Id);
        storage.AdjunctSize.Should().Be(2048);
        
        // Simulate API reacting to updated Adjunct
        await dbContext.Entry(adjunct).ReloadAsync();
        // sanity check - prev op should have updated the Size
        adjunct.Size.Should().Be(2048);
        // we leave size as-is, we change origin though to e.g. say it should be a different file
        adjunct.Origin = origin4k;
        await dbContext.SaveChangesAsync();
        
        message = new IngestAdjunctRequest(adjunct.Id, adjunct.AssetId, DateTime.UtcNow);
        
        // Act 2 - (re)ingest adjunct
        jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        
        result = await httpClient.PostAsync("adjunct-ingest", jsonContent);
        
        // Assert 2
        result.Should().BeSuccessful();
        
        var updatedAdjunct =  await dbContext.Adjuncts.SingleAsync(a => a.Id == adjunctId && a.AssetId == asset.Id);
        updatedAdjunct.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedAdjunct.Ingesting.Should().BeFalse();
        updatedAdjunct.Error.Should().BeEmpty();
        updatedAdjunct.Size.Should().Be(4096);
        
        storage = await dbContext.ImageStorages.SingleAsync(a => a.Id == asset.Id);
        storage.AdjunctSize.Should().Be(4096, "the update origin replaces 2k adjunct with 4k adjunct - should not be any different value");
    }

    [Fact]
    public async Task IngestAdjunct_Success_UpdatesCustomerStorage()
    {
        // Seed per-space storage row before parent-asset ingest so both rows are updated by that ingest
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: 99, space: 1);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var asset = await CreateParentAsset();

        const string adjunctId = nameof(IngestAdjunct_Success_UpdatesCustomerStorage);

        var adjunct = new Adjunct
        {
            Id = adjunctId, AssetId = asset.Id, Asset = asset, IIIFLink = IIIFLinkType.SeeAlso,
            MediaType = "image/jpeg", Type = "Image", Origin = origin2k, Created = DateTime.UtcNow,
            Error = string.Empty, Ingesting = true
        };

        dbContext.Adjuncts.Add(adjunct);
        await dbContext.SaveChangesAsync();

        // Capture before-state after parent-asset ingest so we can do delta checks
        var aggregateRow = await dbContext.CustomerStorages.SingleAsync(cs => cs.Customer == 99 && cs.Space == null);
        var spaceRow = await dbContext.CustomerStorages.SingleAsync(cs => cs.Customer == 99 && cs.Space == 1);
        var imagesSizeBefore = aggregateRow.TotalSizeOfStoredImages;
        var spaceImagesSizeBefore = spaceRow.TotalSizeOfStoredImages;
        var adjunctSizeBefore = aggregateRow.TotalSizeOfStoredAdjuncts;
        var spaceAdjunctSizeBefore = spaceRow.TotalSizeOfStoredAdjuncts;

        var message = new IngestAdjunctRequest(adjunct.Id, adjunct.AssetId, DateTime.UtcNow);
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");

        // Act
        var result = await httpClient.PostAsync("adjunct-ingest", jsonContent);

        // Assert
        result.Should().BeSuccessful();

        await dbContext.Entry(aggregateRow).ReloadAsync();
        await dbContext.Entry(spaceRow).ReloadAsync();

        aggregateRow.TotalSizeOfStoredAdjuncts.Should().Be(adjunctSizeBefore + 2048,
            "hosted adjunct size should be reflected in the aggregate CustomerStorage row");
        aggregateRow.TotalSizeOfStoredImages.Should().Be(imagesSizeBefore,
            "adjunct ingest should not affect the image size column");

        spaceRow.TotalSizeOfStoredAdjuncts.Should().Be(spaceAdjunctSizeBefore + 2048,
            "hosted adjunct size should be reflected in the per-space CustomerStorage row");
        spaceRow.TotalSizeOfStoredImages.Should().Be(spaceImagesSizeBefore,
            "adjunct ingest should not affect the image size column");
    }

    [Fact]
    public async Task IngestAdjunct_MultipleAdjunctsOnOneAsset_CustomerStorageSumsAll()
    {
        // Regression: CustomerStorage previously over-counted when an asset had >1 adjunct (it added the whole
        // cumulative ImageStorage tally each ingest). It should equal the true sum of the adjuncts.
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: 99, space: 1);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var asset = await CreateParentAsset();

        var aggregateRow = await dbContext.CustomerStorages.SingleAsync(cs => cs.Customer == 99 && cs.Space == null);
        var spaceRow = await dbContext.CustomerStorages.SingleAsync(cs => cs.Customer == 99 && cs.Space == 1);
        var adjunctSizeBefore = aggregateRow.TotalSizeOfStoredAdjuncts;
        var spaceAdjunctSizeBefore = spaceRow.TotalSizeOfStoredAdjuncts;

        // Add both adjuncts (FK only, no Asset navigation, so the tracked asset graph isn't re-inserted)
        dbContext.Adjuncts.Add(new Adjunct
        {
            Id = "multi-adjunct-a", AssetId = asset.Id, IIIFLink = IIIFLinkType.SeeAlso, MediaType = "image/jpeg",
            Type = "Image", Origin = origin2k, Created = DateTime.UtcNow, Error = string.Empty, Ingesting = true
        });
        dbContext.Adjuncts.Add(new Adjunct
        {
            Id = "multi-adjunct-b", AssetId = asset.Id, IIIFLink = IIIFLinkType.SeeAlso, MediaType = "image/jpeg",
            Type = "Image", Origin = origin4k, Created = DateTime.UtcNow, Error = string.Empty, Ingesting = true
        });
        await dbContext.SaveChangesAsync();

        // Act - ingest both hosted adjuncts (2048 + 4096) on the same asset
        foreach (var id in new[] { "multi-adjunct-a", "multi-adjunct-b" })
        {
            var message = new IngestAdjunctRequest(id, asset.Id, DateTime.UtcNow);
            var jsonContent = new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
            (await httpClient.PostAsync("adjunct-ingest", jsonContent)).Should().BeSuccessful();
        }

        // Assert
        await dbContext.Entry(aggregateRow).ReloadAsync();
        await dbContext.Entry(spaceRow).ReloadAsync();

        aggregateRow.TotalSizeOfStoredAdjuncts.Should().Be(adjunctSizeBefore + 2048 + 4096,
            "customer storage should equal the sum of both hosted adjuncts");
        spaceRow.TotalSizeOfStoredAdjuncts.Should().Be(spaceAdjunctSizeBefore + 2048 + 4096);

        var storage = await dbContext.ImageStorages.SingleAsync(a => a.Id == asset.Id);
        storage.AdjunctSize.Should().Be(2048 + 4096, "the per-asset ImageStorage tally should also equal the sum");
    }

    [Fact]
    public async Task IngestAdjunct_OptimisedS3Ambient_RecordsSize_WithoutCountingTowardsStorage()
    {
        // Arrange
        var asset = await CreateParentAsset();

        const string adjunctId = nameof(IngestAdjunct_OptimisedS3Ambient_RecordsSize_WithoutCountingTowardsStorage);
        const int adjunctByteCount = 4321;
        var originKey = $"{asset.Id}/optimised-adjunct-source";

        // The adjunct bytes live in the customer's (optimised) origin bucket - Protagonist doesn't copy them
        var s3 = localStack.AWSS3ClientFactory();
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = LocalStackFixture.OriginBucketName,
            Key = originKey,
            InputStream = new MemoryStream(new byte[adjunctByteCount])
        });

        var origin = $"s3://{LocalStackFixture.OriginBucketName}/{originKey}";

        // Register an optimised s3-ambient strategy that matches the origin
        dbContext.CustomerOriginStrategies.Add(new CustomerOriginStrategy
        {
            Id = adjunctId, Customer = asset.Customer, Regex = $"s3://{LocalStackFixture.OriginBucketName}/.*",
            Strategy = OriginStrategyType.S3Ambient, Optimised = true, Order = 0
        });

        var adjunct = new Adjunct
        {
            Id = adjunctId, AssetId = asset.Id, Asset = asset, IIIFLink = IIIFLinkType.SeeAlso,
            MediaType = "image/jpeg", Type = "Image", Origin = origin, Created = DateTime.UtcNow,
            Error = string.Empty, Ingesting = true
        };
        dbContext.Adjuncts.Add(adjunct);
        await dbContext.SaveChangesAsync();

        var message = new IngestAdjunctRequest(adjunct.Id, adjunct.AssetId, DateTime.UtcNow);
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");

        // Act
        var result = await httpClient.PostAsync("adjunct-ingest", jsonContent);

        // Assert
        result.Should().BeSuccessful();

        // Size is recorded from the origin object (via a HEAD request)...
        var updatedAdjunct = await dbContext.Adjuncts.SingleAsync(a => a.Id == adjunctId && a.AssetId == asset.Id);
        updatedAdjunct.Ingesting.Should().BeFalse();
        updatedAdjunct.Error.Should().BeEmpty();
        updatedAdjunct.Size.Should().Be(adjunctByteCount);

        // ...but the bytes aren't stored by Protagonist, so they don't count against storage
        var storage = await dbContext.ImageStorages.SingleAsync(a => a.Id == asset.Id);
        storage.AdjunctSize.Should().Be(0);

        // And no adjunct object is copied into the storage bucket
        BucketWriter.ShouldNotHaveKey($"{asset.Id}/adjuncts/{adjunct.Id}");
    }

    [Fact]
    public async Task IngestAsset_Error_ExceedAllowance()
    {
        // prep customer
        await dbContext.Customers.AddTestCustomer(CustomerForLimits);
        await dbContext.Spaces.AddTestSpace(CustomerForLimits, SpaceExceedLimit);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: CustomerForLimits);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        
        var asset = await CreateParentAsset(customer:CustomerForLimits);

        const string adjunctId = nameof(IngestAsset_Error_ExceedAllowance);
        
        // Note - API would have set this up before handing of
        var adjunct = new Adjunct
        {
            Id = adjunctId, AssetId = asset.Id, Asset = asset, IIIFLink = IIIFLinkType.SeeAlso,
            MediaType = "image/jpeg", Type = "Image", Origin = origin2k, Created = DateTime.UtcNow, Error = string.Empty, Ingesting = true
        };
        
        dbContext.Adjuncts.Add(adjunct);
        
        // also update customer storage to exceed limit
        var customerStorage = dbContext.CustomerStorages.Single(cs => cs.Customer == CustomerForLimits);
        customerStorage.StoragePolicy = "small";
        customerStorage.TotalSizeOfStoredImages = 1000000000L;
        
        dbContext.Entry(customerStorage).State = EntityState.Modified;
        
        await dbContext.SaveChangesAsync();
        
        var message = new IngestAdjunctRequest(adjunct.Id, adjunct.AssetId, DateTime.UtcNow);
        
        // Act
        var jsonContent =
            new StringContent(JsonSerializer.Serialize(message, settings), Encoding.UTF8, "application/json");
        
        var result = await httpClient.PostAsync("adjunct-ingest", jsonContent);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InsufficientStorage);

        // No S3 assets created
        BucketWriter.ShouldNotHaveKey($"{asset.Id}/adjuncts/{adjunct.Id}");

        // Database records updated
        await dbContext.Entry(adjunct).ReloadAsync();

        adjunct.Ingesting.Should().BeFalse();
        adjunct.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(SpaceExceedLimit));
        adjunct.Error.Should().Be("StoragePolicy size limit exceeded");
        
        var storage = await dbContext.ImageStorages.SingleOrDefaultAsync(a => a.Id == asset.Id);
        storage.AdjunctSize.Should().Be(0);
    }
    
    // -- helpers ---

    private async Task<Asset> CreateParentAsset(int customer = 99, int space = 1, [CallerMemberName] string assetName = "",
        string assetPostfix = "")
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
        
        var assetId = AssetIdGenerator.GetAssetId(customer, space, assetName, assetPostfix);

        // Note - API would have set this up before handing off
        var origin = $"{apiStub.Address}/image";
        var entity = await dbContext.Images.AddTestAsset(assetId, customer: customer, space: space, ingesting: true, origin: origin,
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

public class FakeAdjunctSaver : IFileSaver
{
    private readonly Dictionary<string, long> fileSizes = new();

    public FakeAdjunctSaver WithFileSize(string origin, long size)
    {
        fileSizes[origin] = size;
        return this;
    }
    
    public Task<long> SaveResponseToDisk(IOriginItem originItem, OriginResponse originResponse, string destination,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(originItem.Origin is {} origin 
                               && fileSizes.TryGetValue(origin, out var size) ? size : 1000L);
    }
}
