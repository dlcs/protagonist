using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Settings;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Model.Messaging;
using DLCS.Repository;
using DLCS.Repository.Messaging;
using FluentAssertions;
using Hydra.Collections;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Test.Helpers.Http;
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
    private readonly ControllableHttpMessageHandler httpHandler;
    
    public AssetTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        httpHandler = new ControllableHttpMessageHandler();
        
        dbContext = dbFixture.DbContext;
        
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                // swap out our MessageBus for a version with a controllable httpClient
                // What would be more elegant is just replacing the HttpClient but how?
                var messageBusDescriptor = services.FirstOrDefault(
                    descriptor => descriptor.ServiceType == typeof(IMessageBus));
                services.Remove(messageBusDescriptor);
                services.AddScoped<IMessageBus>(GetTestMessageBus);
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

    private IMessageBus GetTestMessageBus(IServiceProvider arg)
    {
        var controllableHttpMessageClient = new HttpClient(httpHandler);
        var options = Options.Create(new DlcsSettings { EngineDirectIngestUri = new Uri("http://engine.dlcs/ingest") });
        var logger = new NullLogger<MessageBus>();
        return new MessageBus(controllableHttpMessageClient, options, logger);
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
    public async Task Put_New_Asset_Creates_Asset()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Creates_Asset));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff""
}}";
        
        HttpRequestMessage engineMessage = null;
        // Register a callback for the API path we're going to call
        httpHandler.RegisterCallback(assetId.ToApiResourcePath(), 
            r => engineMessage = r, 
            "{ \"engine\": \"hello\" }", HttpStatusCode.OK);
        // Register a predicate that will match this path in the request body
        // We need to do this because the request path to Engine that we are
        // intercepting here is just /ingest
        httpHandler.RegisterCallbackSelector(assetId.ToApiResourcePath(),  
            message => message.Content.ReadAsStringAsync().Result.Contains(assetId.Asset));
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        engineMessage.Should().NotBeNull();
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Id.Should().Be(assetId.ToString());

        asset.ThumbnailPolicy.Should().Be("default");
        asset.ImageOptimisationPolicy.Should().Be("fast-higher");
    }
    
    [Fact]
    public async Task Put_New_Asset_Requires_Origin()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Requires_Origin));
        var hydraImageBody = $@"{{
  ""@type"": ""Image""
}}";
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Patch_Asset_Updates_Asset_Without_Calling_Engine()
    {
        // This is the same as Put_Asset_Updates_Asset, but with a PATCH
        var assetId = new AssetId(99, 1, nameof(Patch_Asset_Updates_Asset_Without_Calling_Engine));
        
        var testAsset = await dbContext.Images.AddTestAsset(assetId.ToString(),
            ref1: "I am string 1", origin:"https://images.org/image2.tiff");
        await dbContext.SaveChangesAsync();
        testAsset.State = EntityState.Detached; // need to untrack before update
        
        
        HttpRequestMessage engineMessage = null;
        httpHandler.RegisterCallback(assetId.ToApiResourcePath(), 
            r => engineMessage = r, 
            "{ \"engine\": \"was-called\" }", HttpStatusCode.OK);
        httpHandler.RegisterCallbackSelector(assetId.ToApiResourcePath(),  
            message => message.Content.ReadAsStringAsync().Result.Contains(assetId.Asset));
        
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""string1"": ""I am edited""
}}";
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        engineMessage.Should().BeNull(); // engine not called
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Reference1.Should().Be("I am edited");
        
    }
    
    [Fact]
    public async Task Patch_Asset_Updates_Asset_And_Calls_Engine_if_Reingest_Required()
    {
        var assetId = new AssetId(99, 1, nameof(Patch_Asset_Updates_Asset_And_Calls_Engine_if_Reingest_Required));
        
        var testAsset = await dbContext.Images.AddTestAsset(assetId.ToString(),
            ref1: "I am string 1", origin:$"https://example.org/{assetId.Asset}.tiff");
        
        await dbContext.SaveChangesAsync();
        testAsset.State = EntityState.Detached; // need to untrack before update
        
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}-changed.tiff"",
  ""string1"": ""I am edited""
}}";
        
        HttpRequestMessage engineMessage = null;
        httpHandler.RegisterCallback(assetId.ToApiResourcePath(), 
            r => engineMessage = r, 
            "{ \"engine\": \"was-called\" }", HttpStatusCode.OK);
        httpHandler.RegisterCallbackSelector(assetId.ToApiResourcePath(),  
            message => message.Content.ReadAsStringAsync().Result.Contains(assetId.Asset));
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        engineMessage.Should().NotBeNull(); // engine was called
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Reference1.Should().Be("I am edited");
    }
    
    
    [Fact]
    public async Task Change_ImageOptimisationPolicy_Not_Allowed()
    {
        // This test is really here ready for when this IS allowed! I think it should be.
        var assetId = new AssetId(99, 1, nameof(Change_ImageOptimisationPolicy_Not_Allowed));
        
        var testAsset = await dbContext.Images.AddTestAsset(assetId.ToString(),
            ref1: "I am string 1", origin:"https://images.org/image1.tiff");
        var testPolicy = new DLCS.Model.Assets.ImageOptimisationPolicy
        {
            Id = "test-policy",
            Name = "Test Policy",
            TechnicalDetails = "1010101"
        };
        dbContext.ImageOptimisationPolicies.Add(testPolicy);
        await dbContext.SaveChangesAsync();
        testAsset.State = EntityState.Detached; // need to untrack before update
        
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""imageOptimisationPolicy"": ""http://localhost/imageOptimisationPolicies/test-policy"",
  ""string1"": ""I am edited""
}}";
        
        HttpRequestMessage engineMessage = null;
        httpHandler.RegisterCallback(assetId.ToApiResourcePath(), 
            r => engineMessage = r, 
            "{ \"engine\": \"was-called\" }", HttpStatusCode.OK);
        httpHandler.RegisterCallbackSelector(assetId.ToApiResourcePath(),  
            message => message.Content.ReadAsStringAsync().Result.Contains(assetId.Asset));
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert CURRENT DELIVERATOR
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        engineMessage.Should().BeNull();
        
        // assert THIS IS WHAT IT SHOULD BE!
        // response.StatusCode.Should().Be(HttpStatusCode.OK);
        // engineMessage.Should().NotBeNull();
        // var asset = await dbContext.Images.FindAsync(assetId.ToString());
        // asset.Reference1.Should().Be("I am edited");
    }
    

    
    
    [Fact]
    public async Task Patch_Asset_Returns_Notfound_if_Asset_Missing()
    {
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""string1"": ""I am edited""
}}";
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync("99/1/this-image-is-not-there", content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    
    [Fact]
    public async Task Put_Asset_Returns_InsufficientStorage_if_Policy_Exceeded()
    {
        // This will break other tests so we need a different customer
        // This customer has maxed out their limit of 2!
        const int customer = 599;
        await dbContext.Customers.AddAsync(new DLCS.Model.Customers.Customer
        {
            Created = DateTime.UtcNow,
            Id = customer,
            DisplayName = "TinyUser",
            Name = "tinycustomer",
            Keys = Array.Empty<string>()
        });
        await dbContext.Spaces.AddTestSpace(customer, 1, "tiny-cust-space");
        await dbContext.StoragePolicies.AddAsync(new DLCS.Model.Storage.StoragePolicy()
        {
            Id = "tiny",
            MaximumNumberOfStoredImages = 2,
            MaximumTotalSizeOfStoredImages = 1000000
        });
        await dbContext.CustomerStorages.AddAsync(new DLCS.Model.Storage.CustomerStorage
        {
            StoragePolicy = "tiny",
            Customer = customer, Space = 0,
            TotalSizeOfStoredImages = 0,
            TotalSizeOfThumbnails = 0,
            NumberOfStoredImages = 2
        });
        await dbContext.SaveChangesAsync();
     
        
        var assetId = new AssetId(customer, 1, nameof(Put_Asset_Returns_InsufficientStorage_if_Policy_Exceeded));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff""
}}";
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.InsufficientStorage);
    }
    
    
    [Fact]
    public async Task Patch_Images_Updates_multiple_images()
    {
        
    }
    

    
    
    [Fact]
    public async Task Post_ImageBytes_Ingests_New_Image()
    {
        // requires S3 test
    }
    
    


}