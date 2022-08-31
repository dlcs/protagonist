using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Settings;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Model.Messaging;
using DLCS.Repository;
using DLCS.Repository.Messaging;
using FakeItEasy;
using FluentAssertions;
using Hydra.Collections;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Test.Helpers.Http;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;
using Xunit;

namespace API.Tests.Integration;

public class FakeBucketWriterProvider
{
    // This also needs to be provided as a class fixture, to ensure
    // the same instance is used when calling Engine.
    public FakeBucketWriterProvider()
    {
        BucketWriter = A.Fake<IBucketWriter>();
    }
    public IBucketWriter BucketWriter { get; }
}

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class AssetTests : 
    IClassFixture<ProtagonistAppFactory<Startup>>, 
    IClassFixture<ControllableHttpMessageHandler>, 
    IClassFixture<FakeBucketWriterProvider>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly IBucketWriter bucketWriter;
    
    public AssetTests(
        DlcsDatabaseFixture dbFixture, 
        ProtagonistAppFactory<Startup> factory,
        ControllableHttpMessageHandler httpHandler,
        FakeBucketWriterProvider fakeBucketWriterProvider)
    {
        dbContext = dbFixture.DbContext;
        
        // If the same instance of these two is to be used when calling Engine as is used in the [Fact],
        // they need to be single instances shared across tests. (?)
        
        this.httpHandler = httpHandler;
        bucketWriter = fakeBucketWriterProvider.BucketWriter;
        
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                // swap out our AssetNotificationSender for a version with a controllable httpClient
                // What would be more elegant is just replacing the HttpClient but how?
                var assetNotificationDescriptor = services.FirstOrDefault(
                    descriptor => descriptor.ServiceType == typeof(IAssetNotificationSender));
                services.Remove(assetNotificationDescriptor);
                services.AddScoped<IAssetNotificationSender>(GetTestNotificationSender);
                services.AddSingleton<IBucketWriter>(bucketWriter);
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

    private IAssetNotificationSender GetTestNotificationSender(IServiceProvider arg)
    {
        var controllableHttpMessageClient = new HttpClient(httpHandler);
        var factory = A.Fake<IHttpClientFactory>();
        A.CallTo(() => factory.CreateClient(A<string>._)).Returns(controllableHttpMessageClient);
        
        var options = Options.Create(new DlcsSettings { EngineDirectIngestUri = new Uri("http://engine.dlcs/ingest") });
        var logger = new NullLogger<AssetNotificationSender>();
        return new AssetNotificationSender(factory, options, logger);
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
    
    [Theory]
    [MemberData(nameof(PagedAssetOrdering))]
    public async Task Paged_Assets_Support_Ordering(int space, string assetPage, string field, string[] expectedOrder)
    {
        await AddMultipleAssets(space, nameof(Paged_Assets_Support_Ordering));
        
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
    
    [Fact]
    public async Task Put_New_Asset_Creates_Asset()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Creates_Asset));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff""
}}";
        
        HttpRequestMessage engineMessage = null;
        httpHandler.RegisterCallbackWithSelector(
            assetId.ToApiResourcePath(),
            r => engineMessage = r, 
            message => message.Content.ReadAsStringAsync().Result.Contains(assetId.Asset),
            "{ \"engine\": \"was-called\" }", HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        engineMessage.Should().NotBeNull();
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Id.Should().Be(assetId.ToString());
        asset.MaxUnauthorised.Should().Be(-1);
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
    public async Task Put_New_Asset_Preserves_InitialOrigin()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Preserves_InitialOrigin));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""initialOrigin"": ""s3://my-bucket/{assetId.Asset}.tiff""
}}";

        HttpRequestMessage engineMessage = null;
        httpHandler.RegisterCallbackWithSelector(
            assetId.ToApiResourcePath(),
            r => engineMessage = r, 
            message => message.Content.ReadAsStringAsync().Result.Contains(assetId.Asset),
            "{ \"engine\": \"was-called\" }", HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var bodySentByEngine = await engineMessage.Content.ReadAsStringAsync();
        bodySentByEngine.Should().Contain($@"s3://my-bucket/{assetId.Asset}.tiff");
    }
    
    [Fact]
    public async Task Put_Existing_Asset_ClearsError_AndMarksAsIngesting()
    {
        var assetId = new AssetId(99, 1, nameof(Put_Existing_Asset_ClearsError_AndMarksAsIngesting));
        await dbContext.Images.AddTestAsset(assetId.ToString(), error: "Sample Error");
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff""
}}";

        HttpRequestMessage engineMessage = null;
        httpHandler.RegisterCallbackWithSelector(
            assetId.ToApiResourcePath(),
            r => engineMessage = r, 
            message => message.Content.ReadAsStringAsync().Result.Contains(assetId.Asset),
            "{ \"engine\": \"was-called\" }", HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var assetFromDatabase = await dbContext.Images.SingleOrDefaultAsync(a => a.Id == assetId.ToString());
        assetFromDatabase.Ingesting.Should().BeTrue();
        assetFromDatabase.Error.Should().BeEmpty();
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
        httpHandler.RegisterCallbackWithSelector(
            assetId.ToApiResourcePath(),
            r => engineMessage = r, 
            message => message.Content.ReadAsStringAsync().Result.Contains(assetId.Asset),
            "{ \"engine\": \"was-called\" }", HttpStatusCode.OK);
        
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
        httpHandler.RegisterCallbackWithSelector(
            assetId.ToApiResourcePath(),
            r => engineMessage = r, 
            message => message.Content.ReadAsStringAsync().Result.Contains(assetId.Asset),
            "{ \"engine\": \"was-called\" }", HttpStatusCode.OK);
        
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
    public async Task Patch_Asset_Change_ImageOptimisationPolicy_Not_Allowed()
    {
        // This test is really here ready for when this IS allowed! I think it should be.
        var assetId = new AssetId(99, 1, nameof(Patch_Asset_Change_ImageOptimisationPolicy_Not_Allowed));
        
        var testAsset = await dbContext.Images.AddTestAsset(assetId.ToString(),
            ref1: "I am string 1", origin:"https://images.org/image1.tiff");
        var testPolicy = new DLCS.Model.Assets.ImageOptimisationPolicy
        {
            Id = "test-policy",
            Name = "Test Policy",
            TechnicalDetails = new[] { "1010101" }
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
        httpHandler.RegisterCallbackWithSelector(
            assetId.ToApiResourcePath(),
            r => engineMessage = r, 
            message => message.Content.ReadAsStringAsync().Result.Contains(assetId.Asset),
            "{ \"engine\": \"was-called\" }", HttpStatusCode.OK);
        
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
        // note "member" not "members"
        await AddMultipleAssets(3003, nameof(Patch_Images_Updates_multiple_images));
        var hydraCollectionBody = $@"{{
  ""@type"": ""Collection"",
  ""member"": [
   {{
        ""@type"": ""Image"",
        ""id"": ""asset-0010"",
        ""string1"": ""Asset 10 patched""
   }},
    {{
        ""@type"": ""Image"",
        ""id"": ""asset-0011"",
        ""string1"": ""Asset 11 patched""
    }},
    {{
        ""@type"": ""Image"",
        ""id"": ""asset-0012"",
        ""string1"": ""Asset 12 patched"",
        ""string3"": ""Asset 12 string3 added""
    }}   
  ]
}}";
        
        // act
        var content = new StringContent(hydraCollectionBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync("/customers/99/spaces/3003/images", content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members.Should().HaveCount(3);
        var hydra10 = coll.Members.Single(m =>
            m["@id"].Value<string>().EndsWith("/customers/99/spaces/3003/images/asset-0010"));
        hydra10["string1"].Value<string>().Should().Be("Asset 10 patched");
        var hydra12 = coll.Members.Single(m =>
            m["@id"].Value<string>().EndsWith("/customers/99/spaces/3003/images/asset-0012"));
        hydra12["string1"].Value<string>().Should().Be("Asset 12 patched");
        hydra12["string3"].Value<string>().Should().Be("Asset 12 string3 added");
        
        dbContext.ChangeTracker.Clear();
        var img10 = await dbContext.Images.FindAsync("99/3003/asset-0010");
        img10.Reference1.Should().Be("Asset 10 patched");
        var img12 = await dbContext.Images.FindAsync("99/3003/asset-0012");
        img12.Reference1.Should().Be("Asset 12 patched");
        img12.Reference3.Should().Be("Asset 12 string3 added");
    }

    [Fact]
    public async Task Bulk_Patch_Prevents_Engine_Call()
    {
        var assetId = new AssetId(99, 1, nameof(Bulk_Patch_Prevents_Engine_Call));
        
        var testAsset = await dbContext.Images.AddTestAsset(assetId.ToString(),
            ref1: "I am string 1", origin:$"https://images.org/{assetId.Asset}.tiff");
        await dbContext.SaveChangesAsync();
        testAsset.State = EntityState.Detached; // need to untrack before update
        
        // There's only one member here, but we still don't allow engine-calling changes
        // via collections.
        var hydraCollectionBody = $@"{{
  ""@type"": ""Collection"",
  ""member"": [
   {{
        ""@type"": ""Image"",
        ""id"": ""{assetId.Asset}"",
        ""origin"": ""https://images.org/{assetId.Asset}-PATCHED.tiff"",
        ""string1"": ""PATCHED""
   }}
  ]
}}";
        // act
        var content = new StringContent(hydraCollectionBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync("/customers/99/spaces/1/images", content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_ImageBytes_Ingests_New_Image()
    {
        var assetId = new AssetId(99, 1, nameof(Post_ImageBytes_Ingests_New_Image));
        var hydraBody = await File.ReadAllTextAsync("Direct_Bytes_Upload.json");
        
        // The test just uses the string form, but we want this to validate later calls more easily
        var hydraJson = JsonConvert.DeserializeObject<ImageWithFile>(hydraBody);
        var stream = new MemoryStream(hydraJson.File); // we'll make sure the stream written to S3 is the same length 
        
        // BucketWriter returns success if it writes that stream
        A.CallTo(() =>
                bucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "protagonist-test-origin" && 
                        o.Key == assetId.ToString()), 
                    A<MemoryStream>.That.Matches(s => s.Length == stream.Length),
                    hydraJson.MediaType))
            .Returns(true);
        
        // make a callback for engine
        HttpRequestMessage engineMessage = null;
        httpHandler.RegisterCallbackWithSelector(
            assetId.ToApiResourcePath(),
            r => engineMessage = r, 
            message => message.Content.ReadAsStringAsync().Result.Contains(assetId.Asset),
            "{ \"engine\": \"was-called\" }", HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PostAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        // The image was saved to S3:
        A.CallTo(() =>
                bucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "protagonist-test-origin" && 
                        o.Key == assetId.ToString()), 
                    A<MemoryStream>.That.Matches(s => s.Length == stream.Length),
                    hydraJson.MediaType))
            .MustHaveHappened();
        
        // Engine was called during this process.
        engineMessage.Should().NotBeNull();
        
        // The API created an Image whose origin is the S3 location
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Should().NotBeNull();
        asset.Origin.Should().Be("https://protagonist-test-origin.s3.eu-west-1.amazonaws.com/99/1/Post_ImageBytes_Ingests_New_Image");

    }

    [Theory]
    [InlineData("/customers/99/spaces/381/images?pageSize=50&q={\"string3\": \"16-20\"}", 5)]
    [InlineData("/customers/99/spaces/382/images?pageSize=50&q={\"string2\": \"1-10\"}", 10)]
    [InlineData("/customers/99/spaces/383/images?pageSize=50&q={\"number3\": 2}", 7)]
    [InlineData("/customers/99/spaces/384/images?pageSize=50&q={\"number3\": 2, \"string2\": \"1-10\"}", 3)]
    [InlineData("/customers/99/spaces/385/images?pageSize=50&q={\"number3\": 2}&string2=1-10", 3)]
    public async void Paged_Assets_Can_Be_Queried(string url, int count)
    {
        int space = Convert.ToInt32(url.Split('/')[4]);
        await dbContext.Spaces.AddTestSpace(99, space, $"query-tests-{space}");
        for (int i = 1; i <= 20; i++)
        {
            var padded = i.ToString().PadLeft(4, '0');
            await dbContext.Images.AddTestAsset($"99/{space}/asset-{padded}",
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
}
