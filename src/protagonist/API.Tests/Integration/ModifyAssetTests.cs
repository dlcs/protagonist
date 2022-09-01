using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;
using Xunit;
using AssetFamily = DLCS.Model.Assets.AssetFamily;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ModifyAssetTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    private static readonly IBucketWriter BucketWriter = A.Fake<IBucketWriter>();
    private static readonly IEngineClient EngineClient = A.Fake<IEngineClient>();
    
    public ModifyAssetTests(
        DlcsDatabaseFixture dbFixture, 
        ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;

        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                // swap out our AssetNotificationSender for a version with a controllable httpClient
                // What would be more elegant is just replacing the HttpClient but how?
                services.AddScoped<IEngineClient>(sp => EngineClient);
                services.AddSingleton<IBucketWriter>(BucketWriter);
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
    public async Task Put_NewImageAsset_Creates_Asset()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewImageAsset_Creates_Asset));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Id.Should().Be(assetId.ToString());
        asset.MaxUnauthorised.Should().Be(-1);
        asset.ThumbnailPolicy.Should().Be("default");
        asset.ImageOptimisationPolicy.Should().Be("fast-higher");
    }

    [Fact]
    public async Task Put_NewImageAsset_ReturnsEngineStatusCode_IfEngineRequestFails()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewImageAsset_ReturnsEngineStatusCode_IfEngineRequestFails));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.TooManyRequests);  // Random status to verify it filters down
        
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.Location.Should().BeNull();
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Id.Should().Be(assetId.ToString());
    }
    
    [Fact(Skip = "Is this an expected behaviour?")]
    public async Task Put_SetsError_IfEngineRequestFails()
    {
        var assetId = new AssetId(99, 1, nameof(Put_SetsError_IfEngineRequestFails));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
}}";

        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.InternalServerError);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var assetFromDatabase = await dbContext.Images.SingleOrDefaultAsync(a => a.Id == assetId.ToString());
        assetFromDatabase.Ingesting.Should().BeFalse();
        assetFromDatabase.Error.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task Put_NewAudioAsset_Creates_Asset()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewAudioAsset_Creates_Asset));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
  ""family"": ""T"",
  ""mediaType"": ""audio/mp4""
}}";
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()),
                    A<CancellationToken>._))
            .Returns(true);
        
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");

        // act
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Id.Should().Be(assetId.ToString());
        asset.MaxUnauthorised.Should().Be(-1);
        asset.ThumbnailPolicy.Should().BeEmpty();
        asset.ImageOptimisationPolicy.Should().Be("audio-max");
    }
    
    [Fact]
    public async Task Put_NewVideoAsset_Creates_Asset()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewVideoAsset_Creates_Asset));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
  ""family"": ""T"",
  ""mediaType"": ""video/mp4""
}}";
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()),
                    A<CancellationToken>._))
            .Returns(true);
        
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");

        // act
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Id.Should().Be(assetId.ToString());
        asset.MaxUnauthorised.Should().Be(-1);
        asset.ThumbnailPolicy.Should().BeEmpty();
        asset.ImageOptimisationPolicy.Should().Be("video-max");
    }

    [Fact]
    public async Task Put_NewTimebasedAsset_Returns500_IfEnqueueFails()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewTimebasedAsset_Returns500_IfEnqueueFails));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
  ""family"": ""T"",
  ""mediaType"": ""video/mp4""
}}";
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()),
                    A<CancellationToken>._))
            .Returns(false);
        
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Headers.Location.Should().BeNull();
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Id.Should().Be(assetId.ToString());
    }

    [Fact]
    public async Task Put_New_Asset_Requires_Origin()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Requires_Origin));
        var hydraImageBody = @"{{
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
        var initialOrigin = "s3://my-bucket/{assetId.Asset}.tiff";
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""initialOrigin"": ""{initialOrigin}""
}}";

        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        A.CallTo(() =>
            EngineClient.SynchronousIngest(
                A<IngestAssetRequest>.That.Matches(r =>
                    r.Asset.Id == assetId.ToString() && r.Asset.InitialOrigin == initialOrigin), false,
                A<CancellationToken>._))
            .MustHaveHappened();
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
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
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

    [Theory]
    [InlineData(AssetFamily.Image)]
    [InlineData(AssetFamily.Timebased)] 
    public async Task Patch_Asset_Updates_Asset_Without_Calling_Engine(AssetFamily family)
    {
        var assetId = new AssetId(99, 1, $"{nameof(Patch_Asset_Updates_Asset_Without_Calling_Engine)}{family}");

        var testAsset = await dbContext.Images.AddTestAsset(assetId.ToString(), family: family,
            ref1: "I am string 1", origin: "https://images.org/image2.tiff");
        await dbContext.SaveChangesAsync();
        testAsset.State = EntityState.Detached; // need to untrack before update

        var hydraImageBody = @"{
  ""@type"": ""Image"",
  ""string1"": ""I am edited""
}";
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), false,
                    A<CancellationToken>._))
            .MustNotHaveHappened();
        
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Reference1.Should().Be("I am edited");
    }
    
    [Fact]
    public async Task Patch_ImageAsset_Updates_Asset_And_Calls_Engine_If_Reingest_Required()
    {
        var assetId = new AssetId(99, 1, nameof(Patch_ImageAsset_Updates_Asset_And_Calls_Engine_If_Reingest_Required));

        var testAsset = await dbContext.Images.AddTestAsset(assetId.ToString(),
            ref1: "I am string 1", origin: $"https://example.org/{assetId.Asset}.tiff");
        
        await dbContext.SaveChangesAsync();
        testAsset.State = EntityState.Detached; // need to untrack before update
        
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}-changed.tiff"",
  ""string1"": ""I am edited""
}}";
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), false,
                    A<CancellationToken>._))
            .MustHaveHappened();
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Reference1.Should().Be("I am edited");
    }
    
    [Fact]
    public async Task Patch_TimebasedAsset_Updates_Asset_AndEnqueuesMessage_IfReingestRequired()
    {
        var assetId = new AssetId(99, 1,
            nameof(Patch_TimebasedAsset_Updates_Asset_AndEnqueuesMessage_IfReingestRequired));

        var testAsset = await dbContext.Images.AddTestAsset(assetId.ToString(), family: AssetFamily.Timebased,
            ref1: "I am string 1", origin: $"https://example.org/{assetId.Asset}.mp4", mediaType: "video/mp4");
        
        await dbContext.SaveChangesAsync();
        testAsset.State = EntityState.Detached; // need to untrack before update
        
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}-changed.mp4"",
  ""string1"": ""I am edited""
}}";

        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()),
                    A<CancellationToken>._))
            .Returns(true);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), 
                    A<CancellationToken>._))
            .MustHaveHappened();
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Reference1.Should().Be("I am edited");
        asset.Batch.Should().BeGreaterThan(0);
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

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert CURRENT DELIVERATOR
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), false,
                    A<CancellationToken>._))
            .MustNotHaveHappened();
        
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
    public async Task Patch_Images_Updates_Multiple_Images()
    {
        // note "member" not "members"
        await dbContext.Spaces.AddTestSpace(99, 3003, nameof(Patch_Images_Updates_Multiple_Images));
        await dbContext.Images.AddTestAsset("99/3003/asset-0010", customer: 99, space: 3003, ref1: "Asset 0010",
            ref2: "String2 0010");
        await dbContext.Images.AddTestAsset("99/3003/asset-0011", customer: 99, space: 3003, ref1: "Asset 0011",
            ref2: "String2 0011");
        await dbContext.Images.AddTestAsset("99/3003/asset-0012", customer: 99, space: 3003, ref1: "Asset 0012",
            ref2: "String2 0012");

        await dbContext.SaveChangesAsync();
        
        var hydraCollectionBody = @"{
  ""@type"": ""Collection"",
  ""member"": [
   {
        ""@type"": ""Image"",
        ""id"": ""asset-0010"",
        ""string1"": ""Asset 10 patched""
   },
    {
        ""@type"": ""Image"",
        ""id"": ""asset-0011"",
        ""string1"": ""Asset 11 patched""
    },
    {
        ""@type"": ""Image"",
        ""id"": ""asset-0012"",
        ""string1"": ""Asset 12 patched"",
        ""string3"": ""Asset 12 string3 added""
    }   
  ]
}";
        
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
                BucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "protagonist-test-origin" && 
                        o.Key == assetId.ToString()), 
                    A<MemoryStream>.That.Matches(s => s.Length == stream.Length),
                    hydraJson.MediaType))
            .Returns(true);
        
        // make a callback for engine
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PostAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        // The image was saved to S3:
        A.CallTo(() =>
                BucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "protagonist-test-origin" &&
                        o.Key == assetId.ToString()),
                    A<MemoryStream>.That.Matches(s => s.Length == stream.Length),
                    hydraJson.MediaType))
            .MustHaveHappened();
        
        // Engine was called during this process.
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId.ToString()), false,
                    A<CancellationToken>._))
            .MustHaveHappened();
        
        // The API created an Image whose origin is the S3 location
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = await dbContext.Images.FindAsync(assetId.ToString());
        asset.Should().NotBeNull();
        asset.Origin.Should().Be("https://protagonist-test-origin.s3.eu-west-1.amazonaws.com/99/1/Post_ImageBytes_Ingests_New_Image");
    }
}
