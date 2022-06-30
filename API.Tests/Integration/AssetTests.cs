using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Assets;
using DLCS.Model.Spaces;
using DLCS.Repository;
using FluentAssertions;
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
}