using System;
using System.Linq;
using System.Threading;
using DLCS.Model.Assets;
using DLCS.Repository.Storage;
using Test.Helpers.Data;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Storage;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class ImageStorageXTests
{
    private readonly DlcsContext dbContext;
    public ImageStorageXTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbFixture.CleanUp();
    }

    [Fact]
    private async Task UpsertImageStorageRecord_AddsNewImageStorageRecord()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var imageStorage = new ImageStorage
        {
            Id = assetId,
            Customer = 1,
            Space = 0,
            ThumbnailSize = 100L,
            Size = 100L,
            LastChecked = DateTime.MinValue.ToUniversalTime()
        };

        // act
        await dbContext.ImageStorages.UpsertImageStorageRecord(imageStorage, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // assert
        var imageStorageRecord = dbContext.ImageStorages.Single(x => x.Id == imageStorage.Id);
        imageStorageRecord.Should().BeEquivalentTo(imageStorage);
    }
    
    [Fact]
    private async Task UpsertImageStorageRecord_UpdatesNewImageStorageRecord()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.ImageStorages.AddTestImageStorage(assetId, size: 100L, thumbSize: 100L);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var imageStorage = new ImageStorage
        {
            Id = assetId,
            Customer = 99,
            Space = 1,
            ThumbnailSize = 1000L,
            Size = 1000L,
            LastChecked = DateTime.MaxValue.ToUniversalTime()
        };

        dbContext.ChangeTracker.Clear();

        // act
        await dbContext.ImageStorages.UpsertImageStorageRecord(imageStorage, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // assert
        var imageStorageRecord = dbContext.ImageStorages.Single(x => x.Id == imageStorage.Id);
        imageStorageRecord.Should().BeEquivalentTo(imageStorage);
    }
    
    [Fact]
    private async Task AdjustAdjunctSize_Untouched_IfDelta0()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.ImageStorages.AddTestImageStorage(assetId, adjunctSize: 1000L);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        // Act
        await dbContext.ImageStorages.AdjustAdjunctSize(assetId, 0, CancellationToken.None);

        // Assert
        var record = dbContext.ImageStorages.Single(x => x.Id == assetId);
        record.AdjunctSize.Should().Be(1000L);
    }
    
    [Fact]
    private async Task AdjustAdjunctSize_DecrementsAdjunctSize()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.ImageStorages.AddTestImageStorage(assetId, adjunctSize: 1000L);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        // Act
        await dbContext.ImageStorages.AdjustAdjunctSize(assetId, -400L, CancellationToken.None);

        // Assert
        var record = dbContext.ImageStorages.Single(x => x.Id == assetId);
        record.AdjunctSize.Should().Be(600L);
    }
    
    [Fact]
    private async Task AdjustAdjunctSize_IncrementsAdjunctSize()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.ImageStorages.AddTestImageStorage(assetId, adjunctSize: 1000L);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        // Act
        await dbContext.ImageStorages.AdjustAdjunctSize(assetId, 400L, CancellationToken.None);

        // Assert
        var record = dbContext.ImageStorages.Single(x => x.Id == assetId);
        record.AdjunctSize.Should().Be(1400L);
    }

    [Fact]
    private async Task AdjustAdjunctSize_ClampsToZero_WhenDecrementExceedsCurrentSize()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.ImageStorages.AddTestImageStorage(assetId, adjunctSize: 100L);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        // Act
        await dbContext.ImageStorages.AdjustAdjunctSize(assetId, -500L, CancellationToken.None);

        // Assert
        var record = dbContext.ImageStorages.Single(x => x.Id == assetId);
        record.AdjunctSize.Should().Be(0L, "result is clamped to 0 rather than going negative");
    }
}
