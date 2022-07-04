using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using API.Features.Image.Requests;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Assets;
using DLCS.Repository;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;
using Xunit;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class AssetTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    
    public AssetTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task Get_Assets_In_Space_Returns_NotFound_For_Missing_Space()
    {
        var getUrl = "/customers/99/spaces/123/images";
        var response = await httpClient.AsCustomer(99).GetAsync(getUrl);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_Assets_In_Space_Returns_Assets()
    {
        // Arrange
        await dbContext.Spaces.AddTestSpace(99, 2, "test-space");
        var id = "99/2/asset1";
        await dbContext.Images.AddTestAsset(id, space:2);
        await dbContext.SaveChangesAsync();
        var getUrl = "/customers/99/spaces/2/images";
        var response = await httpClient.AsCustomer(99).GetAsync(getUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    

    
    [Fact]
    public async Task PutAsset_Saves_New_Asset()
    {
        var id = "new-asset";
        var newAsset = new Asset { Id = id, Customer = 100, Space = 10, Reference1 = "I am new"};
        
        var result = AssetPreparer.PrepareAssetForUpsert(null, newAsset, false);
        result.Success.Should().BeTrue();
        
        await PutAsset(newAsset);

        var dbAsset = await dbContext.Images.FindAsync(id);
        dbAsset.Reference1.Should().Be("I am new");
        dbAsset.Reference2.Should().Be("");
        dbAsset.MediaType.Should().Be("unknown");
    }
    
    [Fact]
    public async Task PutAsset_Saves_Existing_Asset()
    {
        var id = "existing-asset";
        // previously
        var assetEntry = await dbContext.Images.AddTestAsset(id, ref1:"I am original 1", ref2:"I am original 2");
        await dbContext.SaveChangesAsync();
        assetEntry.State = EntityState.Detached;
        
        var existingAsset = await dbContext.Images.AsNoTracking().FirstAsync(a => a.Id == id);
        var patch = new Asset { Id = id };
        // change something:
        patch.Reference1 = "I am changed";
        
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, patch, false);
        result.Success.Should().BeTrue();
        
        await PutAsset(patch);

        var dbAsset = await dbContext.Images.FindAsync(id);
        dbAsset.Reference1.Should().Be("I am changed");
        dbAsset.Reference2.Should().Be("I am original 2");
    }
    
    
    [Fact]
    public async Task PutAsset_Saves_Tracked_Asset()
    {
        var id = "tracked-asset";
        // previously
        var assetEntry = await dbContext.Images.AddTestAsset(id, ref1:"I am original 1", ref2:"I am original 2");
        await dbContext.SaveChangesAsync();

        var trackedAsset = assetEntry.Entity;
        trackedAsset.Reference1 = "I am changed";
        
        var result = AssetPreparer.PrepareAssetForUpsert(null, trackedAsset,
            true); // <-- note this is true for a tracked asset...
        result.Success.Should().BeTrue();
        
        await PutAsset(trackedAsset);

        var dbAsset = await dbContext.Images.FindAsync(id);
        dbAsset.Reference1.Should().Be("I am changed");
        dbAsset.Reference2.Should().Be("I am original 2");
    }

    private async Task PutAsset(Asset putAsset)
    {
        if (dbContext.Images.Local.Any(asset => asset.Id == putAsset.Id))
        {
            // asset with this ID is already being tracked
            if (dbContext.Entry(putAsset).State == EntityState.Detached)
            {
                // but it ain't this instance!
                // what do we do?
                return;
            }

            // just save it I guess - it is this instance?
            await dbContext.SaveChangesAsync();
            return;

        }
        
        var existing = await dbContext.Images.FindAsync(putAsset.Id);
        if (existing == null)
        {
            await dbContext.Images.AddAsync(putAsset);
        }
        else
        {
            dbContext.Images.Update(putAsset);
        }
        await dbContext.SaveChangesAsync();
    }
}