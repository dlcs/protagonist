using System;
using System.Linq;
using System.Threading;
using DLCS.Model.Assets;
using DLCS.Repository.Storage;
using Microsoft.EntityFrameworkCore;
using Test.Helpers.Data;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Storage;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class ImageStorageRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly ImageStorageRepository sut;

    public ImageStorageRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        
        sut = new ImageStorageRepository(dbContext);
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
        await sut.UpsertImageStorageRecord(imageStorage, CancellationToken.None);
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
        await sut.UpsertImageStorageRecord(imageStorage, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // assert
        var imageStorageRecord = dbContext.ImageStorages.Single(x => x.Id == imageStorage.Id);
        imageStorageRecord.Should().BeEquivalentTo(imageStorage);
    }
}
