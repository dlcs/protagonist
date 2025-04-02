using DLCS.Core.Types;
using Microsoft.EntityFrameworkCore;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Assets;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class AssetTests
{
    private readonly DlcsContext dbContext;
    
    public AssetTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task Asset_Manifest_TestGinIndex()
    {
        // Arrange and Act
        await dbContext.Images.AddTestAsset(new AssetId(1050, 2, $"{nameof(Asset_Manifest_TestGinIndex)}_1"),
            manifests: ["first"]);
        await dbContext.Images.AddTestAsset(new AssetId(1050, 2, $"{nameof(Asset_Manifest_TestGinIndex)}_2"),
            manifests: ["first", "second"]);
        
        await dbContext.SaveChangesAsync();
        
        // this query is the same as what you would get from using .Where
        var assets = dbContext.Images.FromSqlRaw("SELECT * FROM \"Images\" Where \"Manifests\" @> ARRAY['second']");
        assets.Should().HaveCount(1);
    }
}
