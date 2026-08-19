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
using DLCS.Web.Response;
using Hydra.Collections;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Test.Helpers.Data;
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
        var getUrl = "/customers/99/spaces/1/images/no-such-asset";
        
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
        hydraImage.Adjuncts!.ToString().Should().Be($"http://localhost/customers/99/spaces/1/images/{modelId}/adjuncts");
        hydraImage.Manifest.Should().Be($"https://dlcs.digirati.io/iiif-manifest/99/1/{modelId}");
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
        coll.View!.Page.Should().Be(1);
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
            view!.Previous.Should().Contain("page=" + pageCounter);
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
        var actual = coll.Members!.Select(m => m[field].Value<string>()).Take(expectedOrder.Length);
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
    [InlineData("nonexistent")]
    [InlineData("ItemId")] // readonly prop on Asset
    [InlineData("HasRoles")] // readonly prop on Asset
    [InlineData("RolesList")] // [NotMapped], can't be translated to EF query
    [InlineData("TagsList")] // [NotMapped], can't be translated to EF query
    [InlineData("imageService")] // a Hydra model property but not a database-backed one
    [InlineData("x")] // previously silently ignored, falling back to created ordering
    [InlineData("adjuncts")] // a collection of related entities cannot be ordered on
    public async Task Get_Paged_Assets_Returns_400_For_Unknown_OrderBy_Field(string orderBy)
    {
        // Act - ordering is validated before anything is fetched so the space doesn't need to exist
        var response = await httpClient.AsCustomer(99).GetAsync(
            $"/customers/99/spaces/3061/images?pageSize=10&orderBy={orderBy}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadAsJsonAsync<Hydra.Model.Error>(ensureSuccess: false);
        error.Detail.Should().Be($"Cannot order by field '{orderBy}'");
    }

    [Theory]
    [InlineData("WIDTH", 3062)] // field matching ignores case
    [InlineData("manifests", 3063)] // primitive-collection columns can be ordered on
    [InlineData("finished", 3064)]
    [InlineData("STRING3", 3065)] // converted to Reference3 internally
    public async Task Get_Paged_Assets_Returns_200_For_Known_OrderBy_Field(string orderBy, int space)
    {
        // Arrange
        await dbContext.Spaces.AddTestSpace(99, space, $"orderby-tests-{space}");
        await dbContext.Images.AddTestAsset(AssetId.FromString($"99/{space}/asset-0001"),
            customer: 99, space: space);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.AsCustomer(99).GetAsync(
            $"/customers/99/spaces/{space}/images?pageSize=10&orderBy={orderBy}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

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
        coll.Members!.Length.Should().Be(count);
    }
    
    [Fact]
    public async Task Get_SpaceImages_Adjuncts_IsUriString_WhenNoIncludeParam()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(customer: 204, space: 201);
        await dbContext.Spaces.AddTestSpace(assetId.Customer, assetId.Space);
        await dbContext.Images.AddTestAsset(assetId, customer: assetId.Customer, space: assetId.Space)
            .WithTestAdjunct("adj1");
        await dbContext.SaveChangesAsync();
        var url = $"/customers/{assetId.Customer}/spaces/{assetId.Space}/images";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>();
        var asset = collection.Members!.Single();
        asset.Adjuncts!.Type.Should().Be(JTokenType.String);
        asset.Adjuncts.ToString().Should().EndWith($"/images/{assetId.Asset}/adjuncts");
    }

    [Fact]
    public async Task Get_SpaceImages_Adjuncts_IsUriString_WhenUnknownIncludeParam()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(customer: 2042, space: 1201);
        await dbContext.Spaces.AddTestSpace(assetId.Customer, assetId.Space);
        await dbContext.Images.AddTestAsset(assetId, customer: assetId.Customer, space: assetId.Space)
            .WithTestAdjunct("adj1");
        await dbContext.SaveChangesAsync();
        var url = $"/customers/{assetId.Customer}/spaces/{assetId.Space}/images?include=something";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>();
        collection.Members!.Single().Adjuncts!.Type.Should().Be(JTokenType.String);
    }

    [Fact]
    public async Task Get_SpaceImages_Adjuncts_IsEmptyArray_WhenIncludeAdjunctsButNoneExist()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(customer: 202, space: 101);
        await dbContext.Spaces.AddTestSpace(assetId.Customer, assetId.Space);
        await dbContext.Images.AddTestAsset(assetId, customer: assetId.Customer, space: assetId.Space);
        await dbContext.SaveChangesAsync();
        var url = $"/customers/{assetId.Customer}/spaces/{assetId.Space}/images?include=adjuncts";
        
        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>();
        var asset = collection.Members!.Single();
        asset.Adjuncts!.Type.Should().Be(JTokenType.Array);
        ((JArray)asset.Adjuncts).Should().BeEmpty();
    }

    [Fact]
    public async Task Get_SpaceImages_Adjuncts_IsInlineArray_WhenIncludeAdjunctsAndAdjunctsExist()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(customer: 203, space: 102);
        await dbContext.Spaces.AddTestSpace(assetId.Customer, assetId.Space);
        await dbContext.Images.AddTestAsset(assetId, customer: assetId.Customer, space: assetId.Space)
            .WithTestAdjunct("adj1")
            .WithTestAdjunct("adj2");
        await dbContext.SaveChangesAsync();
        var url = $"/customers/{assetId.Customer}/spaces/{assetId.Space}/images?include=adjuncts";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>();
        var asset = collection.Members!.Single();
        asset.Adjuncts!.Type.Should().Be(JTokenType.Array);
        ((JArray)asset.Adjuncts).Should().HaveCount(2);
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
