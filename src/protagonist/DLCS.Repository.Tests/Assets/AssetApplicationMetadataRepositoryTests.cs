using System;
using DLCS.Core.Types;
using DLCS.Model.Assets.Metadata;
using DLCS.Repository.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Assets;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class AssetApplicationMetadataRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly AssetApplicationMetadataRepository sut;

    public AssetApplicationMetadataRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;

        var optionsBuilder = new DbContextOptionsBuilder<DlcsContext>();
        optionsBuilder.UseNpgsql(dbFixture.ConnectionString);
        var contextForTests = new DlcsContext(optionsBuilder.Options);
        
        sut = new AssetApplicationMetadataRepository(contextForTests, new NullLogger<AssetApplicationMetadataRepository>());
        
        dbFixture.CleanUp();
        dbContext.Images.AddTestAsset(AssetId.FromString("99/1/1"), ref1: "foobar");
        dbContext.SaveChanges();
    }
    
    [Fact]
    public async Task DeleteAssetApplicationMetadata_DeletesMetadata_WhenCalled()
    {
        // Arrange
        var assetId = AssetId.FromString("99/1/1");
        
        var assetApplicationMetadata = new AssetApplicationMetadata
        {
            AssetId = assetId,
            MetadataType = AssetApplicationMetadataTypes.ThumbSizes,
            MetadataValue = "{\"a\": [], \"o\": [[75, 100], [150, 200], [300, 400], [769, 1024]]}",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        };
        await dbContext.AssetApplicationMetadata.AddAsync(assetApplicationMetadata);
        await dbContext.SaveChangesAsync();
        
        // Act
        var metadata = await sut.DeleteAssetApplicationMetadata(assetId, AssetApplicationMetadataTypes.ThumbSizes);

        var metaDataFromDatabase = await dbContext.AssetApplicationMetadata.FirstOrDefaultAsync(x =>
            x.AssetId == assetId && x.MetadataType == AssetApplicationMetadataTypes.ThumbSizes);
        
        // Assert
        metadata.Should().BeTrue();
        metaDataFromDatabase.Should().BeNull();
    }
    
    [Fact]
    public async Task DeleteAssetApplicationMetadata_DoesNotDeleteMetadata_WhenCalledWithNonexistentMetadata()
    {
        // Arrange
        var assetId = AssetId.FromString("99/1/1");
        
        // Act
        var metadata = await sut.DeleteAssetApplicationMetadata(assetId, AssetApplicationMetadataTypes.ThumbSizes);
        
        // Assert
        metadata.Should().BeFalse();
    }
}
