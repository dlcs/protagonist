using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Repository;
using Hydra.Collections;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class GetAssetTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    
    public GetAssetTests(
        DlcsDatabaseFixture dbFixture, 
        ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;

        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                services.AddAuthentication("API-Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "API-Test", _ => { });
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        dbFixture.CleanUp();
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
        var id = AssetId.FromString($"99/1/{modelId}");
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
    public async Task Get_Assets_In_Space_Returns_Page_of_Assets()
    {
        // GET PAGE OF IMAGES
        var id = AssetId.FromString("99/2998/asset1");
        await dbContext.Spaces.AddTestSpace(99, 2998, "Space 2998");
        await dbContext.Images.AddTestAsset(id, space:2998);
        await dbContext.SaveChangesAsync();
        var getUrl = "/customers/99/spaces/2998/images";
        var response = await httpClient.AsCustomer(99).GetAsync(getUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task Get_Paged_Assets_Return_Correct_Views()
    {
        await AddMultipleAssets(3001, nameof(Get_Paged_Assets_Return_Correct_Views));
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
    
    [Theory]
    [MemberData(nameof(PagedAssetOrdering))]
    public async Task Get_Paged_Assets_Support_Ordering(int space, string assetPage, string field, string[] expectedOrder)
    {
        await AddMultipleAssets(space, nameof(Get_Paged_Assets_Support_Ordering));
        
        // Act
        var response = await httpClient.AsCustomer(99).GetAsync(assetPage);

        // Assert
        var coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        var actual = coll.Members.Select(m => m[field].Value<string>()).Take(expectedOrder.Length);
        actual.Should().BeEquivalentTo(expectedOrder, opts => opts.WithStrictOrdering());
    }    
    
    public static IEnumerable<object[]> PagedAssetOrdering => new List<object[]>
    {
        new object[]
        {
            3051,
            "/customers/99/spaces/3051/images?pageSize=10&orderBy=string1", "string1",
            new[] { "Asset 0001", "Asset 0002" }
        },
        new object[]
        {
            3052,
            "/customers/99/spaces/3052/images?pageSize=10&orderByDescending=string1", "string1",
            new[] { "Asset 0035", "Asset 0034" }
        },
        new object[]
        {
            3053,
            "/customers/99/spaces/3053/images?page=2&pageSize=10&orderByDescending=string1", "string1",
            new[] { "Asset 0025", "Asset 0024" }
        },
        new object[]
        {
            3054,
            "/customers/99/spaces/3054/images?pageSize=10&orderByDescending=width", "width",
            new[] { "2004", "2004" }
        },
        new object[]
        {
            3055,
            "/customers/99/spaces/3055/images?pageSize=10&orderByDescending=number2", "string1",
            new[] { "Asset 0001", "Asset 0002" }
        }
    };
    
    [Theory]
    [InlineData("/customers/99/spaces/381/images?pageSize=50&q={\"string3\": \"16-20\"}", 5)]
    [InlineData("/customers/99/spaces/382/images?pageSize=50&q={\"string2\": \"1-10\"}", 10)]
    [InlineData("/customers/99/spaces/383/images?pageSize=50&q={\"number3\": 2}", 7)]
    [InlineData("/customers/99/spaces/384/images?pageSize=50&q={\"number3\": 2, \"string2\": \"1-10\"}", 3)]
    [InlineData("/customers/99/spaces/385/images?pageSize=50&q={\"number3\": 2}&string2=1-10", 3)]
    public async Task Get_Paged_Assets_Can_Be_Queried(string url, int count)
    {
        int space = Convert.ToInt32(url.Split('/')[4]);
        await dbContext.Spaces.AddTestSpace(99, space, $"query-tests-{space}");
        for (int i = 1; i <= 20; i++)
        {
            var padded = i.ToString().PadLeft(4, '0');
            await dbContext.Images.AddTestAsset(AssetId.FromString($"99/{space}/asset-{padded}"),
                customer: 99, space: space,
                num1: i, num2: i % 2, num3: i % 3,
                ref1: $"Asset {padded}",
                ref2: i < 11 ? "1-10" : "11-20",
                ref3: i < 16 ? "1-15" : "16-20");
        }
        await dbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(99).GetAsync(url);

        // Assert
        var coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members.Length.Should().Be(count);
    }
    
    private async Task AddMultipleAssets(int space, string name)
    {
        await dbContext.Spaces.AddTestSpace(99, space, name);
        for (int i = 1; i <= 35; i++)
        {
            var padded = i.ToString().PadLeft(4, '0');
            await dbContext.Images.AddTestAsset(AssetId.FromString($"99/{space}/asset-{padded}"),
                customer: 99, space: space,
                width: 2000 + i % 5,
                height: 3000 + i % 6,
                num1: i, num2: 100 - i,
                ref1: $"Asset {padded}",
                ref2: $"String2 {100 - i}");
        }

        await dbContext.SaveChangesAsync();
    }
}
