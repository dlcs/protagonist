using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using Engine.Data;
using Engine.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Integration;

namespace Engine.Tests.Data;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class EngineAssetRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly DlcsContext contextForTests;
    private readonly EngineAssetRepository sut;

    public EngineAssetRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;

        var optionsBuilder = new DbContextOptionsBuilder<DlcsContext>();
        optionsBuilder.UseNpgsql(dbFixture.ConnectionString);
        contextForTests = new DlcsContext(optionsBuilder.Options);
        sut = new EngineAssetRepository(contextForTests, new NullLogger<EngineAssetRepository>());
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_ReturnsFalse_IfError()
    {
        var assetId = AssetId.FromString($"99/1/{nameof(UpdateIngestedAsset_ReturnsFalse_IfError)}");
        var entry = await dbContext.Images.AddTestAsset(assetId, width: 10, height: 20, duration: 30,
            ingesting: true, ref1: "foo", roles: "secret");
        var existingAsset = entry.Entity;
        await dbContext.SaveChangesAsync();

        // Omit required fields
        var newAsset = new Asset(assetId);
        
        // Act
        var success = await sut.UpdateIngestedAsset(newAsset, null, null, true);
        
        // Assert
        success.Should().BeFalse();
        var updatedItem = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedItem.Width.Should().Be(existingAsset.Width);
        updatedItem.Height.Should().Be(existingAsset.Height);
        updatedItem.Duration.Should().Be(existingAsset.Duration);
        updatedItem.Error.Should().Be(existingAsset.Error);
    }

    [Fact]
    public async Task UpdateIngestedAsset_ModifiedExistingAsset_NoBatch_Location_OrStorage()
    {
        // Arrange
        var assetId =
            AssetId.FromString($"99/1/{nameof(UpdateIngestedAsset_ModifiedExistingAsset_NoBatch_Location_OrStorage)}");
        var entry = await dbContext.Images.AddTestAsset(assetId, width: 0, height: 0, duration: 0,
            ingesting: true, ref1: "foo", roles: "secret");
        var existingAsset = entry.Entity;
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = 0, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = "broken state"
        };
        
        // Act
        var success = await sut.UpdateIngestedAsset(newAsset, null, null, true);
        
        // Assert
        success.Should().BeTrue();
        
        var updatedItem = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedItem.Width.Should().Be(newAsset.Width);
        updatedItem.Height.Should().Be(newAsset.Height);
        updatedItem.Duration.Should().Be(newAsset.Duration);
        updatedItem.Error.Should().Be(newAsset.Error);
        updatedItem.Ingesting.Should().BeFalse();
        updatedItem.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedItem.MediaType.Should().Be(existingAsset.MediaType, "MediaType not set on newAsset so not updated");
        updatedItem.Reference1.Should().Be(existingAsset.Reference1, "Reference1 not changed");
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_UpdatesAlreadyTrackedAsset()
    {
        // Arrange
        var assetId = AssetId.FromString($"99/1/{nameof(UpdateIngestedAsset_UpdatesAlreadyTrackedAsset)}");
        await dbContext.Images.AddTestAsset(assetId, width: 0, height: 0, duration: 0,
            ingesting: true, ref1: "foo", roles: "secret");
        await dbContext.SaveChangesAsync();

        // Get asset so that it is tracked 
        var trackedAsset = await sut.GetAsset(assetId);
        trackedAsset.Width = 999;
        trackedAsset.Height = 1000;
        trackedAsset.Duration = 99;
        trackedAsset.Error = "broken state";

        // Act
        var success = await sut.UpdateIngestedAsset(trackedAsset, null, null, true);
        
        // Assert
        trackedAsset.Should().NotBeNull();
        
        success.Should().BeTrue();
        
        var updatedItem = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedItem.Width.Should().Be(trackedAsset.Width);
        updatedItem.Height.Should().Be(trackedAsset.Height);
        updatedItem.Duration.Should().Be(trackedAsset.Duration);
        updatedItem.Error.Should().Be(trackedAsset.Error);
        updatedItem.Ingesting.Should().BeFalse();
        updatedItem.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_ModifiedExistingAsset_IncludingMediaType_NoBatch_Location_OrStorage()
    {
        // Arrange
        var assetId =
            AssetId.FromString(
                $"99/1/{nameof(UpdateIngestedAsset_ModifiedExistingAsset_IncludingMediaType_NoBatch_Location_OrStorage)}");
        var entry = await dbContext.Images.AddTestAsset(assetId, width: 0, height: 0, duration: 0,
            ingesting: true, ref1: "foo", roles: "secret");
        var existingAsset = entry.Entity;
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = 0, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = "broken state"
        };

        // Act
        var success = await sut.UpdateIngestedAsset(newAsset, null, null, true);
        
        // Assert
        success.Should().BeTrue();
        
        var updatedItem = await dbContext.Images.AsNoTracking().SingleAsync(a => a.Id == assetId);
        updatedItem.Width.Should().Be(newAsset.Width);
        updatedItem.Height.Should().Be(newAsset.Height);
        updatedItem.Duration.Should().Be(newAsset.Duration);
        updatedItem.Error.Should().Be(newAsset.Error);
        updatedItem.Ingesting.Should().BeFalse();
        updatedItem.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedItem.MediaType.Should().Be(existingAsset.MediaType, "MediaType not set on newAsset so not updated");
        updatedItem.Reference1.Should().Be(existingAsset.Reference1, "Reference1 not changed");
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_ModifiedExistingAsset_IgnoresMediaTypeIfDefaultValue()
    {
        // Arrange
        var assetId =
            AssetId.FromString(
                $"99/1/{nameof(UpdateIngestedAsset_ModifiedExistingAsset_IgnoresMediaTypeIfDefaultValue)}");
        var entry = await dbContext.Images.AddTestAsset(assetId, width: 0, height: 0, duration: 0,
            ingesting: true, ref1: "foo", roles: "secret", mediaType: "application/json");
        var existingAsset = entry.Entity;
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = 0, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = "broken state", MediaType = "unknown"
        };
        
        // Act
        var success = await sut.UpdateIngestedAsset(newAsset, null, null, true);
        
        // Assert
        success.Should().BeTrue();
        
        var updatedItem = await dbContext.Images.AsNoTracking().SingleAsync(a => a.Id == assetId);
        updatedItem.Width.Should().Be(newAsset.Width);
        updatedItem.Height.Should().Be(newAsset.Height);
        updatedItem.Duration.Should().Be(newAsset.Duration);
        updatedItem.Error.Should().Be(newAsset.Error);
        updatedItem.Ingesting.Should().BeFalse();
        updatedItem.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedItem.MediaType.Should().Be(existingAsset.MediaType, "MediaType not set on newAsset so not updated");
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_ModifiedExistingAsset_NoBatch_WithLocationAndStorage_NoExistingLocationOrStorage()
    {
        // Arrange
        var assetId =
            AssetId.FromString(
                $"99/1/{nameof(UpdateIngestedAsset_ModifiedExistingAsset_NoBatch_WithLocationAndStorage_NoExistingLocationOrStorage)}");
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = 0, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = "broken state", MediaType = "foo/bar"
        };

        var imageLocation = new ImageLocation { Id = assetId, S3 = "union-card", Nas = "wedding-coat" };
        var imageStorage = new ImageStorage
        {
            Id = assetId, Customer = 99, Space = 1, Size = 1010, CheckingInProgress = false,
            LastChecked = DateTime.UtcNow, ThumbnailSize = 2020
        };
        
        // Act
        var success = await sut.UpdateIngestedAsset(newAsset, imageLocation, imageStorage, true);
        
        // Assert
        success.Should().BeTrue();
        
        var dbImageLocation = await dbContext.ImageLocations.SingleAsync(a => a.Id == assetId);
        dbImageLocation.Should().BeEquivalentTo(imageLocation);
        var dbImageStorage = await dbContext.ImageStorages.SingleAsync(a => a.Id == assetId);
        dbImageStorage.Should().BeEquivalentTo(imageStorage, opts => opts.Excluding(s => s.LastChecked));
        dbImageStorage.LastChecked.Should().BeCloseTo(imageStorage.LastChecked, TimeSpan.FromMinutes(1));
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_ModifiedExistingAsset_NoBatch_WithLocationAndStorage_ExistingLocationOrStorage()
    {
        // Arrange
        var assetId =
            AssetId.FromString(
                $"99/1/{nameof(UpdateIngestedAsset_ModifiedExistingAsset_NoBatch_WithLocationAndStorage_ExistingLocationOrStorage)}");
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.ImageLocations.AddTestImageLocation(assetId);
        await dbContext.ImageStorages.AddTestImageStorage(assetId);
        await dbContext.CustomerStorages.AddTestCustomerStorage(sizeOfStored: 500, sizeOfThumbs: 800);
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = 0, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = "broken state", MediaType = "foo/bar"
        };

        var imageLocation = new ImageLocation { Id = assetId, S3 = "union-card", Nas = "wedding-coat" };
        var imageStorage = new ImageStorage
        {
            Id = assetId, Customer = 99, Space = 1, Size = 1010, CheckingInProgress = false,
            LastChecked = DateTime.UtcNow, ThumbnailSize = 2020
        };
        
        // Act
        var success = await sut.UpdateIngestedAsset(newAsset, imageLocation, imageStorage, true);
        
        // Assert
        success.Should().BeTrue();
        
        var dbImageLocation = await dbContext.ImageLocations.SingleAsync(a => a.Id == assetId);
        dbImageLocation.Should().BeEquivalentTo(imageLocation);
        
        var dbImageStorage = await dbContext.ImageStorages.SingleAsync(a => a.Id == assetId);
        dbImageStorage.Should().BeEquivalentTo(imageStorage, opts => opts.Excluding(s => s.LastChecked));
        dbImageStorage.LastChecked.Should().BeCloseTo(imageStorage.LastChecked, TimeSpan.FromMinutes(1));

        var dbCustomerStorage = await dbContext.CustomerStorages.SingleAsync(cs => cs.Customer == 99 && cs.Space == 0);
        dbCustomerStorage.TotalSizeOfStoredImages.Should().Be(1510);
        dbCustomerStorage.TotalSizeOfThumbnails.Should().Be(2820);
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_UpdatesBatch_IfError()
    {
        // Arrange
        var assetId = AssetId.FromString($"99/1/{nameof(UpdateIngestedAsset_UpdatesBatch_IfError)}");
        const int batchId = -10;
        await dbContext.Batches.AddTestBatch(batchId, count: 10, errors: 1, completed: 1);
        await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = batchId, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = "broken state"
        };

        // Act
        var success = await sut.UpdateIngestedAsset(newAsset, null, null, true);
        
        // Assert
        success.Should().BeTrue();
        
        var updatedItem = await dbContext.Batches.SingleAsync(b => b.Id == batchId);
        updatedItem.Errors.Should().Be(2);
        updatedItem.Completed.Should().Be(1);
        updatedItem.Finished.Should().BeNull();
    }
    
    [Trait("Category", "Manual")]
    [Fact]
    public async Task UpdateIngestedAsset_UpdatesBatch_HandlesExistingTransaction()
    {
        // Arrange
        var assetId = AssetId.FromString($"99/1/{nameof(UpdateIngestedAsset_UpdatesBatch_HandlesExistingTransaction)}");
        const int batchId = -10;
        await dbContext.Batches.AddTestBatch(batchId, count: 10, errors: 1, completed: 1);
        await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = batchId, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = "broken state"
        };

        // Act
        await using var transaction = await contextForTests.Database.BeginTransactionAsync();
        var success = await sut.UpdateIngestedAsset(newAsset, null, null, true);
        await transaction.CommitAsync();
        
        // Assert
        success.Should().BeTrue();
        
        var updatedItem = await dbContext.Batches.SingleAsync(b => b.Id == batchId);
        updatedItem.Errors.Should().Be(2);
        updatedItem.Completed.Should().Be(1);
        updatedItem.Finished.Should().BeNull();
    }
    
    [Trait("Category", "Manual")]
    [Fact]
    public async Task UpdateIngestedAsset_UpdatesBatch_HandlesExistingTransactionRollback()
    {
        // Arrange
        var assetId = AssetId.FromString($"99/1/{nameof(UpdateIngestedAsset_UpdatesBatch_HandlesExistingTransactionRollback)}");
        const int batchId = -10;
        await dbContext.Batches.AddTestBatch(batchId, count: 10, errors: 1, completed: 1);
        await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = batchId, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = "broken state"
        };

        // Act
        await using var transaction = await contextForTests.Database.BeginTransactionAsync();
        var success = await sut.UpdateIngestedAsset(newAsset, null, null, true);
        await transaction.RollbackAsync();
        
        // Assert
        success.Should().BeTrue();
        
        var updatedItem = await dbContext.Batches.SingleAsync(b => b.Id == batchId);
        updatedItem.Errors.Should().Be(1);
        updatedItem.Completed.Should().Be(1);
        updatedItem.Finished.Should().BeNull();
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_UpdatesBatch_IfComplete()
    {
        // Arrange
        var assetId = AssetId.FromString($"99/1/{nameof(UpdateIngestedAsset_UpdatesBatch_IfComplete)}");
        const int batchId = -11;
        await dbContext.Batches.AddTestBatch(batchId, count: 10, errors: 1, completed: 1);
        await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = batchId, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = string.Empty
        };
        
        // Act
        var success = await sut.UpdateIngestedAsset(newAsset, null, null, true);
        
        // Assert
        success.Should().BeTrue();
        
        var updatedItem = await dbContext.Batches.SingleAsync(b => b.Id == batchId);
        updatedItem.Errors.Should().Be(1);
        updatedItem.Completed.Should().Be(2);
        updatedItem.Finished.Should().BeNull();
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_DoesNotUpdateBatch_IfIngestNotFinished()
    {
        // Arrange
        var assetId = AssetId.FromString($"99/1/{nameof(UpdateIngestedAsset_DoesNotUpdateBatch_IfIngestNotFinished)}");
        const int batchId = -111;
        await dbContext.Batches.AddTestBatch(batchId, count: 10, errors: 1, completed: 1);
        await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = batchId, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = string.Empty
        };
        
        // Act
        var success = await sut.UpdateIngestedAsset(newAsset, null, null, false);
        
        // Assert
        success.Should().BeTrue();
        
        var updatedBatch = await dbContext.Batches.SingleAsync(b => b.Id == batchId);
        updatedBatch.Errors.Should().Be(1);
        updatedBatch.Completed.Should().Be(1);
        updatedBatch.Finished.Should().BeNull();
        
        var updatedImage = await dbContext.Images.SingleAsync(i => i.Id == assetId);
        updatedImage.Finished.Should().BeNull();
        updatedImage.Ingesting.Should().BeTrue();
    }
    
    [Theory]
    [InlineData(-12, "" )]
    [InlineData(-13, "error")]
    public async Task UpdateIngestedAsset_MarksBatchAsComplete_IfCompletedAndError_EqualsCount(int batchId, string error)
    {
        // Arrange
        var assetId =
            AssetId.FromString(
                $"99/1/{nameof(UpdateIngestedAsset_MarksBatchAsComplete_IfCompletedAndError_EqualsCount)}{batchId}");
        await dbContext.Batches.AddTestBatch(batchId, count: 10, errors: 1, completed: 8);
        await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = batchId, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = string.Empty
        };
        
        // Act
        var success = await sut.UpdateIngestedAsset(newAsset, null, null, true);
        
        // Assert
        success.Should().BeTrue();
        
        var updatedItem = await dbContext.Batches.SingleAsync(b => b.Id == batchId);
        updatedItem.Finished.Should().NotBeNull();
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_SavesError_IfBatchNotFound()
    {
        var assetId = AssetId.FromString($"99/1/{nameof(UpdateIngestedAsset_SavesError_IfBatchNotFound)}");
        const int batchId = -100;
        await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.SaveChangesAsync();

        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = batchId, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = string.Empty
        };
        
        // Act
        var success = await sut.UpdateIngestedAsset(newAsset, null, null, true);
        
        // Assert
        success.Should().BeTrue();
        
        var updatedItem = await dbContext.Images.AsNoTracking().SingleAsync(a => a.Id == assetId);
        updatedItem.Error.Should().Be("Unable to update batch associated with image");
    }
}
