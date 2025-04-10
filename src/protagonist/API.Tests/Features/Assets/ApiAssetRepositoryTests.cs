using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using API.Features.Assets;
using API.Tests.Integration.Infrastructure;
using DLCS.Core;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Entities;
using LazyCache.Mocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace API.Tests.Features.Assets;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ApiAssetRepositoryTests
{
    private readonly DlcsContext contextForTests;
    private readonly DlcsContext dbContext;
    private readonly ApiAssetRepository sut;
    private readonly char[] restrictedCharacters = Array.Empty<char>();

    public ApiAssetRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        // Store non-tracking dbContext for adding items to backing store + verifying results
        contextForTests = dbFixture.DbContext;
        
        // We use a customised dbcontext for SUT because we want different tracking behaviour
        dbContext = new DlcsContext(
            new DbContextOptionsBuilder<DlcsContext>()
                .UseNpgsql(dbFixture.ConnectionString).Options
        );
        // We want this turned on to match live behaviour
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        var entityCounterRepo = new EntityCounterRepository(dbContext, new NullLogger<EntityCounterRepository>());

        var assetRepositoryCachingHelper = new AssetCachingHelper(
            new MockCachingService(),
            Options.Create(new CacheSettings()),
            new NullLogger<AssetCachingHelper>()
        );

        sut = new ApiAssetRepository(dbContext, entityCounterRepo, assetRepositoryCachingHelper,
            new NullLogger<ApiAssetRepository>());

        dbFixture.CleanUp();
    }

    [Fact]
    public async Task AssetRepository_Saves_New_Asset()
    {
        var assetId = AssetId.FromString("100/10/new-asset");
        var newAsset = new Asset(assetId)
        {
            Reference1 = "I am new", Origin = "https://example.org/image1.tiff",
            DeliveryChannels = Array.Empty<string>()
        };
    
        var result = AssetPreparer.PrepareAssetForUpsert(null, newAsset, false, false, restrictedCharacters);
        result.Success.Should().BeTrue();

        await sut.Save(newAsset, false, CancellationToken.None);

        var dbAsset = await dbContext.Images.FindAsync(assetId);
        dbAsset.Reference1.Should().Be("I am new");
        dbAsset.Reference2.Should().Be("");
        dbAsset.MediaType.Should().Be("unknown");
    }
    
    [Fact]
    public async Task AssetRepository_Saves_New_Asset_IncrementsCounter_CountersDoNotExist()
    {
        var assetId = AssetId.FromString($"1010/99/{nameof(AssetRepository_Saves_New_Asset_IncrementsCounter_CountersDoNotExist)}");
        var newAsset = new Asset(assetId)
        {
            Reference1 = "I am new", Origin = "https://example.org/image1.tiff",
            DeliveryChannels = Array.Empty<string>()
        };
    
        var result = AssetPreparer.PrepareAssetForUpsert(null, newAsset, false, false, restrictedCharacters);
        result.Success.Should().BeTrue();

        // Act
        await sut.Save(newAsset, false, CancellationToken.None);

        // Assert
        var dbAsset = await contextForTests.Images.FindAsync(assetId);
        dbAsset.Reference1.Should().Be("I am new");
        dbAsset.Reference2.Should().Be("");
        dbAsset.MediaType.Should().Be("unknown");

        var customerCounter = await contextForTests.EntityCounters.SingleAsync(ec =>
            ec.Customer == 0 && ec.Type == "customer-images" && ec.Scope == "1010");
        customerCounter.Next.Should().Be(1);
        var spaceCounter = await contextForTests.EntityCounters.SingleAsync(ec =>
            ec.Customer == 1010 && ec.Type == "space-images" && ec.Scope == "99");
        spaceCounter.Next.Should().Be(1);
    }
    
    [Fact]
    public async Task AssetRepository_Saves_New_Asset_IncrementsCounter_CountersExist()
    {
        var assetId = AssetId.FromString($"10120/99/{nameof(AssetRepository_Saves_New_Asset_IncrementsCounter_CountersExist)}");
        var newAsset = new Asset(assetId)
        {
            Reference1 = "I am new", Origin = "https://example.org/image1.tiff",
            DeliveryChannels = Array.Empty<string>()
        };
        var customerCounter = await contextForTests.EntityCounters.AddAsync(
            new EntityCounter { Customer = 0, Scope = "10120", Next = 100, Type = "customer-images" });
        var spaceCounter = await contextForTests.EntityCounters.AddAsync(
            new EntityCounter { Customer = 10120, Scope = "99", Next = 10, Type = "space-images" });
        await contextForTests.SaveChangesAsync();
    
        var result = AssetPreparer.PrepareAssetForUpsert(null, newAsset, false, false, restrictedCharacters);
        result.Success.Should().BeTrue();

        // Act
        await sut.Save(newAsset, false, CancellationToken.None);

        // Assert
        var dbAsset = await contextForTests.Images.FindAsync(assetId);
        dbAsset.Reference1.Should().Be("I am new");
        dbAsset.Reference2.Should().Be("");
        dbAsset.MediaType.Should().Be("unknown");
        
        await dbContext.Entry(customerCounter.Entity).ReloadAsync();
        customerCounter.Entity.Next.Should().Be(101);
        
        await dbContext.Entry(spaceCounter.Entity).ReloadAsync();
        spaceCounter.Entity.Next.Should().Be(11);
    }
    
    [Fact]
    public async Task AssetRepository_Saves_New_Asset_UsingResultFromPreparer()
    {
        var assetId = AssetId.FromString($"100/10/{nameof(AssetRepository_Saves_New_Asset_UsingResultFromPreparer)}");
        var newAsset = new Asset(assetId)
        {
            Reference1 = "I am new", Origin = "https://example.org/image1.tiff", DeliveryChannels = Array.Empty<string>()
        };
    
        var result = AssetPreparer.PrepareAssetForUpsert(null, newAsset, false, false, restrictedCharacters);
        result.Success.Should().BeTrue();

        await sut.Save(result.UpdatedAsset!, false, CancellationToken.None);

        var dbAsset = await contextForTests.Images.FindAsync(assetId);
        dbAsset.Reference1.Should().Be("I am new");
        dbAsset.Reference2.Should().Be("");
        dbAsset.MediaType.Should().Be("unknown");
    }
    
    [Fact]
    public async Task AssetRepository_Saves_Existing_Asset()
    {
        // Arrange
        var assetId = AssetId.FromString($"100/10/{nameof(AssetRepository_Saves_Existing_Asset)}");
        var dbAsset =
            await contextForTests.Images.AddTestAsset(assetId, ref1: "I am original 1",
                ref2: "I am original 2");
        await contextForTests.SaveChangesAsync();

        var existingAsset = await dbContext.Images.FirstAsync(a => a.Id == assetId);
        var patch = new Asset
        {
            Id = assetId,
            Reference1 = "I am changed",
            Customer = 99,
            Space = 1
        };
        
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, patch, false, false, restrictedCharacters);
        result.Success.Should().BeTrue();
    
        // Act
        await sut.Save(existingAsset, true, CancellationToken.None);

        await contextForTests.Entry(dbAsset.Entity).ReloadAsync();
        dbAsset.Entity.Reference1.Should().Be("I am changed");
        dbAsset.Entity.Reference2.Should().Be("I am original 2");
    }

    [Fact]
    public async Task AssetRepository_Saves_Existing_Asset_DoesNotIncrementCounters()
    {
        // Arrange
        var assetId = AssetId.FromString($"1100/10/{nameof(AssetRepository_Saves_Existing_Asset)}");
        var dbAsset =
            await contextForTests.Images.AddTestAsset(assetId, ref1: "I am original 1",
                ref2: "I am original 2");
        var customerCounter = await contextForTests.EntityCounters.AddAsync(
            new EntityCounter { Customer = 0, Scope = "1100", Next = 100, Type = "customer-images" });
        var spaceCounter = await contextForTests.EntityCounters.AddAsync(
            new EntityCounter { Customer = 1100, Scope = "10", Next = 10, Type = "space-images" });
        await contextForTests.SaveChangesAsync();
        await contextForTests.SaveChangesAsync();

        var existingAsset = await dbContext.Images.FirstAsync(a => a.Id == assetId);
        var patch = new Asset
        {
            Id = assetId,
            Reference1 = "I am changed",
            Customer = 99,
            Space = 1
        };
        
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, patch, false, false, restrictedCharacters);
        result.Success.Should().BeTrue();
    
        // Act
        await sut.Save(existingAsset, true, CancellationToken.None);

        await contextForTests.Entry(dbAsset.Entity).ReloadAsync();
        dbAsset.Entity.Reference1.Should().Be("I am changed");
        dbAsset.Entity.Reference2.Should().Be("I am original 2");
        
        await dbContext.Entry(customerCounter.Entity).ReloadAsync();
        customerCounter.Entity.Next.Should().Be(100);
        
        await dbContext.Entry(spaceCounter.Entity).ReloadAsync();
        spaceCounter.Entity.Next.Should().Be(10);
    }

    [Fact]
    public async Task AssetRepository_Saves_Existing_Asset_UsingResultFromPreparer()
    {
        // Arrange
        var assetId =
            AssetId.FromString($"100/10/{nameof(AssetRepository_Saves_Existing_Asset_UsingResultFromPreparer)}");
        var dbAsset = await contextForTests.Images.AddTestAsset(assetId, ref1: "I am original 1",
                ref2: "I am original 2");
        await contextForTests.SaveChangesAsync();

        var existingAsset = await dbContext.Images.FirstAsync(a => a.Id == assetId);
        var patch = new Asset
        {
            Id = assetId,
            Reference1 = "I am changed",
            Customer = 99,
            Space = 1
        };
        
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, patch, false, false, restrictedCharacters);
        result.Success.Should().BeTrue();
    
        // Act
        await sut.Save(result.UpdatedAsset, true, CancellationToken.None);

        await contextForTests.Entry(dbAsset.Entity).ReloadAsync();
        dbAsset.Entity.Reference1.Should().Be("I am changed");
        dbAsset.Entity.Reference2.Should().Be("I am original 2");
    }
    
    [Fact]
    public async Task AssetRepository_SavesExistingAsset_WithRestrictedCharters()
    {
        // Arrange
        var assetId = AssetId.FromString($"100/10/id with restricted characters");
        var dbAsset =
            await contextForTests.Images.AddTestAsset(assetId, ref1: "I am original 1",
                ref2: "I am original 2");
        await contextForTests.SaveChangesAsync();

        var existingAsset = await dbContext.Images.FirstAsync(a => a.Id == assetId);
        var patch = new Asset
        {
            Id = assetId,
            Reference1 = "I am changed",
            Customer = 99,
            Space = 1
        };
        
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, patch, false, false, new []{' '});
        result.Success.Should().BeTrue();
    
        // Act
        await sut.Save(existingAsset, true, CancellationToken.None);

        await contextForTests.Entry(dbAsset.Entity).ReloadAsync();
        dbAsset.Entity.Reference1.Should().Be("I am changed");
        dbAsset.Entity.Reference2.Should().Be("I am original 2");
    }
    
    [Fact]
    public void AssetRepository_FailsToSaveAsset_WhichHasRestrictedCharacters()
    {
        var assetId = AssetId.FromString("100/10/id with restricted characters 2");
        var newAsset = new Asset(assetId)
        {
            Reference1 = "I am new", Origin = "https://example.org/image1.tiff",
            DeliveryChannels = Array.Empty<string>()
        };
    
        var result = AssetPreparer.PrepareAssetForUpsert(null, newAsset, false, false, new []{' '});
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Asset id contains at least one of the following restricted characters. Valid values are:  ");
    }

    [Fact]
    public async Task DeleteAsset_ReturnsCorrectStatus_IfNotFound()
    {
        // Arrange
        var assetId = AssetId.FromString($"100/10/{nameof(DeleteAsset_ReturnsCorrectStatus_IfNotFound)}");
        
        // Act
        var result = await sut.DeleteAsset(assetId);
        
        // Assert
        result.Result.Should().Be(DeleteResult.NotFound);
        result.DeletedEntity.Should().BeNull();
    }
    
    [Fact]
    public async Task DeleteAsset_ReturnsCorrectStatus_IfAssetFoundButNoImageLocation()
    {
        // Arrange
        var assetId = AssetId.FromString($"100/10/{nameof(DeleteAsset_ReturnsCorrectStatus_IfAssetFoundButNoImageLocation)}");
        await contextForTests.Images.AddTestAsset(assetId);
        await contextForTests.SaveChangesAsync();
        
        // Act
        var result = await sut.DeleteAsset(assetId);
        
        // Assert
        result.Result.Should().Be(DeleteResult.Deleted);
    }
    
    [Fact]
    public async Task DeleteAsset_ReturnsCorrectStatus_IfDeleted()
    {
        // Arrange
        var assetId = AssetId.FromString($"100/10/{nameof(DeleteAsset_ReturnsCorrectStatus_IfDeleted)}");
        var dbAsset = await contextForTests.Images.AddTestAsset(assetId);
        await contextForTests.ImageLocations.AddTestImageLocation(assetId);
        await contextForTests.SaveChangesAsync();
        
        // Act
        var result = await sut.DeleteAsset(assetId);
        
        // Assert
        result.Result.Should().Be(DeleteResult.Deleted);
        result.DeletedEntity.Should()
            .BeEquivalentTo(dbAsset.Entity, options => options
                    .Excluding(a => a.Created)
                    .Excluding(a => a.ImageDeliveryChannels),
                "returned object is as deleted, exclude created as datetime can be off by a few ms");
        result.DeletedEntity.Created.Should().BeCloseTo(dbAsset.Entity.Created.Value, TimeSpan.FromSeconds(1));

        contextForTests.Images.Any(i => i.Id == assetId).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsset_ReturnsImageDeliveryChannels_FromDeletedAsset()
    {
        // Arrange
        var assetId = AssetId.FromString($"100/10/{nameof(DeleteAsset_ReturnsImageDeliveryChannels_FromDeletedAsset)}");
        await contextForTests.Images.AddTestAsset(assetId, imageDeliveryChannels: new List<ImageDeliveryChannel>
        {
            new()
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
            },
            new()
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault
            },
            new()
            {
                Channel = AssetDeliveryChannels.File,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
            }
        });
        await contextForTests.SaveChangesAsync();
        
        // Act
        var result = await sut.DeleteAsset(assetId);
        
        // Assert
        result.Result.Should().Be(DeleteResult.Deleted);
        result.DeletedEntity!.ImageDeliveryChannels.Count.Should().Be(3);
        result.DeletedEntity!.ImageDeliveryChannels.Should().Satisfy(
            i => i.Channel == AssetDeliveryChannels.Image,
            i => i.Channel == AssetDeliveryChannels.Thumbnails,
            i => i.Channel == AssetDeliveryChannels.File
        );
    }
}
