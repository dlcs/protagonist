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
    private readonly DlcsContext contextForTests;
    
    public AssetApplicationMetadataRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;

        var optionsBuilder = new DbContextOptionsBuilder<DlcsContext>();
        optionsBuilder.UseNpgsql(dbFixture.ConnectionString);
        contextForTests = new DlcsContext(optionsBuilder.Options);
        
        sut = new AssetApplicationMetadataRepository(contextForTests, new NullLogger<AssetApplicationMetadataRepository>());
        
        dbFixture.CleanUp();
        dbContext.Images.AddTestAsset(AssetId.FromString("99/1/1"), ref1: "foobar");
        dbContext.SaveChanges();
    }
    
    [Fact]
    public async Task UpsertApplicationMetadata_AddsMetadata_WhenCalledWithNew()
    {
        // Arrange
        var assetId = AssetId.FromString("99/1/1");
        var metadataValue = "{\"a\": [], \"o\": [[75, 100], [150, 200], [300, 400], [769, 1024]]}";
        
        // Act
        var metadata = await sut.UpsertApplicationMetadata(assetId, 
            AssetApplicationMetadataTypes.ThumbSizes, metadataValue);

        var metaDataFromDatabase = await dbContext.AssetApplicationMetadata.FirstAsync(x =>
            x.AssetId == assetId && x.MetadataType == AssetApplicationMetadataTypes.ThumbSizes);
        
        // Assert
        metadata.Should().NotBeNull();
        metaDataFromDatabase.Should().NotBeNull();
        metaDataFromDatabase.MetadataValue.Should().Be(metadataValue);
    }
    
    [Fact]
    public async Task UpsertApplicationMetadata_UpdatesMetadata_WhenCalledWithUpdated()
    {
        // Arrange
        var assetId = AssetId.FromString("99/1/1");
        await sut.UpsertApplicationMetadata(assetId, 
            AssetApplicationMetadataTypes.ThumbSizes, "{\"a\": [], \"o\": []}");
        var newMetadataValue = "{\"a\": [], \"o\": [[75, 100], [150, 200], [300, 400], [769, 1024]]}";
        
        // Act
        var metadata = await sut.UpsertApplicationMetadata(assetId, 
            AssetApplicationMetadataTypes.ThumbSizes, newMetadataValue);
        var metaDataFromDatabase = await contextForTests.AssetApplicationMetadata.FirstAsync(x =>
            x.AssetId == assetId && x.MetadataType == AssetApplicationMetadataTypes.ThumbSizes);
        
        // Assert
        metadata.Should().NotBeNull();
        metaDataFromDatabase.Should().NotBeNull();
        metaDataFromDatabase.MetadataValue.Should().Be(newMetadataValue);
    }
    
    [Fact]
    public async Task UpsertApplicationMetadata_ThrowsException_WhenCalledWithInvalidJson()
    {
        // Arrange
        var assetId = AssetId.FromString("99/1/1");
        var metadataValue = "not json";
        
        // Act
        Func<Task> action = async () => await sut.UpsertApplicationMetadata(assetId, 
            AssetApplicationMetadataTypes.ThumbSizes, metadataValue);

        // Assert
        await action.Should().ThrowAsync<DbUpdateException>();
    }
}