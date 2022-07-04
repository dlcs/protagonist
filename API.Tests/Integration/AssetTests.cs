using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using API.Client;
using API.Features.Image.Requests;
using API.Tests.Integration.Infrastructure;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Assets;
using FluentAssertions;
using Hydra.Collections;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
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

    private async Task AddMultipleAssets(int space, string name)
    {
        await dbContext.Spaces.AddTestSpace(99, space, name);
        for (int i = 1; i <= 35; i++)
        {
            var padded = i.ToString().PadLeft(4, '0');
            await dbContext.Images.AddTestAsset($"99/{space}/asset-{padded}",
                customer: 99, space: space,
                width: 2000 + i % 5,
                height: 3000 + i % 6,
                num1: i, num2: 100 - i,
                ref1: $"Asset {padded}",
                ref2: $"String2 {100 - i}");
        }

        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_Asset_Returns_NotFound_for_Missing_Asset()
    {
        // GET IMAGE
        // arrange
        var getUrl = $"/customers/99/spaces/1/images/no-such-asset";
        
        // act
        var response = await httpClient.AsCustomer(99).GetAsync(getUrl);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    
    [Fact]
    public async Task Get_Asset_Returns_Asset()
    {
        // GET IMAGE
        // arrange
        var modelId = nameof(Get_Asset_Returns_Asset);
        var id = $"99/1/{modelId}";
        await dbContext.Images.AddTestAsset(id, customer:99, space:1);
        await dbContext.SaveChangesAsync();
        var getUrl = $"/customers/99/spaces/1/images/{modelId}";
        
        // act
        var response = await httpClient.AsCustomer(99).GetAsync(getUrl);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var hydraImage = await response.ReadAsHydraResponseAsync<Image>();
        hydraImage.Id.Should().EndWith(getUrl);
    }
    
    [Fact]
    public async Task Get_Assets_In_Space_Returns_NotFound_For_Missing_Space()
    {
        // GET PAGE OF IMAGES
        var getUrl = "/customers/99/spaces/123/images";
        var response = await httpClient.AsCustomer(99).GetAsync(getUrl);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_Assets_In_Space_Returns_Assets()
    {
        // GET PAGE OF IMAGES
        // Arrange
        await dbContext.Spaces.AddTestSpace(99, 2999, "test-space");
        var id = "99/2999/asset1";
        await dbContext.Images.AddTestAsset(id, space:2999);
        await dbContext.SaveChangesAsync();
        var getUrl = "/customers/99/spaces/2999/images";
        var response = await httpClient.AsCustomer(99).GetAsync(getUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task Get_Assets_In_Space_Returns_Page_of_Assets()
    {
        // GET PAGE OF IMAGES
        var id = "99/2998/asset1";
        await dbContext.Spaces.AddTestSpace(99, 2998, "Space 2998");
        await dbContext.Images.AddTestAsset(id, space:2998);
        await dbContext.SaveChangesAsync();
        var getUrl = "/customers/99/spaces/2998/images";
        var response = await httpClient.AsCustomer(99).GetAsync(getUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task Paged_Assets_Return_Correct_Views()
    {
        await AddMultipleAssets(3001, nameof(Paged_Assets_Return_Correct_Views));
        // arrange
        // set a pageSize of 10
        var assetPage = "/customers/99/spaces/3001/images?pageSize=10";
        
        // act
        var response = await httpClient.AsCustomer(99).GetAsync(assetPage);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Should().NotBeNull();
        coll.Type.Should().Be("Collection");
        coll.Members.Should().HaveCount(10);
        coll.PageSize.Should().Be(10);
        coll.View.Should().NotBeNull();
        coll.View.Page.Should().Be(1);
        coll.View.Previous.Should().BeNull();
        coll.View.Next.Should().Contain("page=2");
        coll.View.TotalPages.Should().Be(4);
        int pageCounter = 1;
        var view = coll.View;
        while (view.Next != null)
        {
            var nextResp = await httpClient.AsCustomer(99).GetAsync(view.Next);
            var nextColl = await nextResp.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
            view = nextColl.View;
            view.Previous.Should().Contain("page=" + pageCounter);
            pageCounter++;
            if (pageCounter < 4)
            {
                nextColl.Members.Should().HaveCount(10);
                view.Next.Should().Contain("page=" + (pageCounter + 1));
            }
            else
            {
                nextColl.Members.Should().HaveCount(5);
                view.Next.Should().BeNull();
            }
        }
    }
    
    [Fact]
    public async Task Paged_Assets_Support_Ordering()
    {
        await AddMultipleAssets(3002, nameof(Paged_Assets_Support_Ordering));
        // arrange
        // set a pageSize of 10
        var assetPage = "/customers/99/spaces/3002/images?pageSize=10&orderBy=string1";
        
        // act
        var response = await httpClient.AsCustomer(99).GetAsync(assetPage);
        
        // assert
        var coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members[0]["string1"].ToString().Should().Be("Asset 0001");
        coll.Members[1]["string1"].ToString().Should().Be("Asset 0002");
        
        assetPage = $"/customers/99/spaces/3002/images?pageSize=10&orderByDescending=string1";
        response = await httpClient.AsCustomer(99).GetAsync(assetPage);
        coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members[0]["string1"].ToString().Should().Be("Asset 0035");
        coll.Members[1]["string1"].ToString().Should().Be("Asset 0034");
        
        var nextPage = await httpClient.AsCustomer(99).GetAsync(coll.View.Next);
        coll = await nextPage.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members[0]["string1"].ToString().Should().Be("Asset 0025");
        coll.Members[1]["string1"].ToString().Should().Be("Asset 0024");
        
        
        assetPage = $"/customers/99/spaces/3002/images?pageSize=10&orderByDescending=width";
        response = await httpClient.AsCustomer(99).GetAsync(assetPage);
        coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members[0]["width"].Value<int>().Should().Be(2004);
        coll.Members[1]["width"].Value<int>().Should().Be(2004);
        
        
        assetPage = $"/customers/99/spaces/3002/images?pageSize=10&orderByDescending=number2";
        response = await httpClient.AsCustomer(99).GetAsync(assetPage);
        coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members[0]["string1"].ToString().Should().Be("Asset 0001");
        coll.Members[1]["string1"].ToString().Should().Be("Asset 0002");

    }

    [Fact]
    public async Task Put_Asset_Creates_Asset()
    {
        
    }
    
    
    [Fact]
    public async Task Put_Asset_Updates_Asset()
    {
        
    }
    
    
    [Fact]
    public async Task Patch_Asset_Updates_Asset()
    {
        
    }
    
    
    [Fact]
    public async Task Patch_Asset_Returns_Notfound_if_Asset_Missing()
    {
        
    }
    
    
    [Fact]
    public async Task Put_Asset_Returns_InsufficientStorage_if_Policy_Exceeded()
    {
        
    }
    
    
    [Fact]
    public async Task Put_Asset_Triggers_Engine_Call()
    {
        
    }
    
    
    [Fact]
    public async Task Patch_Asset_Does_Not_Trigger_Engine_Call_if_Simple_Change()
    {
        
    }
    
    [Fact]
    public async Task Patch_Asset_Triggers_Engine_Call_if_Processing_Change()
    {
        
    }
    
    
    
    [Fact]
    public async Task Patch_Images_Updates_multiple_images()
    {
        
    }
    
    
    [Fact]
    public async Task Patch_Images_Prevents_Reprocessing_Calls()
    {
        
    }

    
    
    [Fact]
    public async Task Post_ImageBytes_Ingests_New_Image()
    {
        // requires S3 test
    }
    
    


}