using DLCS.Model.Assets.Metadata;
using DLCS.Repository;
using Engine.Data;
using Engine.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Test.Helpers.Data;
using Test.Helpers.Integration;

namespace Engine.Tests.Data;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class AssetApplicationMetadataXTests
{
    private readonly DlcsContext dbContext;
    
    public AssetApplicationMetadataXTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;

        var optionsBuilder = new DbContextOptionsBuilder<DlcsContext>();
        optionsBuilder.UseNpgsql(dbFixture.ConnectionString);
        
        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task UpsertApplicationMetadata_AddsMetadata_WhenCalledWithNew()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = (await dbContext.Images.AddTestAsset(assetId)).Entity;
        await dbContext.SaveChangesAsync();
        
        var metadataValue = "{\"a\": [], \"o\": [[75, 100], [150, 200], [300, 400], [769, 1024]]}";
        
        // Act
        var metadata = asset.UpsertApplicationMetadata(AssetApplicationMetadataTypes.ThumbSizes, metadataValue);
        await dbContext.SaveChangesAsync();
        
        // Assert
        var metaDataFromDatabase = await dbContext.AssetApplicationMetadata.SingleAsync(md =>
            md.AssetId == assetId && md.MetadataType == AssetApplicationMetadataTypes.ThumbSizes);
        
        metadata.Should().NotBeNull();
        metaDataFromDatabase.Should().NotBeNull();
        metaDataFromDatabase.MetadataValue.Should().Be(metadataValue);
    }
    
    [Fact]
    public async Task UpsertApplicationMetadata_UpdatesMetadata_WhenCalledWithUpdated()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = (await dbContext.Images.AddTestAsset(assetId).WithTestThumbnailMetadata()).Entity;
        await dbContext.SaveChangesAsync();
        var newMetadataValue = "{\"a\": [], \"o\": [[75, 100], [150, 200], [300, 400], [769, 1024]]}";
        
        // Act
        var metadata = asset.UpsertApplicationMetadata(AssetApplicationMetadataTypes.ThumbSizes, newMetadataValue);
        await dbContext.SaveChangesAsync();
        
        // Assert
        var metaDataFromDatabase = await dbContext.AssetApplicationMetadata.SingleAsync(md =>
            md.AssetId == assetId && md.MetadataType == AssetApplicationMetadataTypes.ThumbSizes);
        metadata.Should().NotBeNull();
        metaDataFromDatabase.Should().NotBeNull();
        metaDataFromDatabase.MetadataValue.Should().Be(newMetadataValue, "Metadata value updated");
    }
    
    [Fact]
    public async Task UpsertApplicationMetadata_ThrowsException_WhenCalledWithInvalidJson()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = (await dbContext.Images.AddTestAsset(assetId)).Entity;
        await dbContext.SaveChangesAsync();
        var metadataValue = "not json";
        
        // Act
        Action action = () => asset.UpsertApplicationMetadata(AssetApplicationMetadataTypes.ThumbSizes, metadataValue);

        // Assert
        action.Should().Throw<JsonReaderException>();
    }
}
