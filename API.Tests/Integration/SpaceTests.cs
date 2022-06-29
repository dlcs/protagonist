using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.HydraModel;
using DLCS.Repository;
using FluentAssertions;
using Hydra;
using Hydra.Collections;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;
using Xunit;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]

public class SpaceTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;    
    
    public SpaceTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbFixture.CleanUp();
    }

    /// <summary>
    /// </summary>
    [Fact]
    public async Task Post_SimpleSpace_Creates_Space()
    {
        // arrange
        int? customerId = 99;
        var counter = await dbContext.EntityCounters.SingleAsync(
                ec => ec.Customer == 99 && ec.Scope == "99" && ec.Type == "space");
        int expectedSpace = (int) counter.Next;
        
        const string newSpaceJson = @"{
  ""@type"": ""Space"",
  ""name"": ""Test Space""
}";
        // act
        var content = new StringContent(newSpaceJson, Encoding.UTF8, "application/json");
        var postUrl = $"/customers/{customerId}/spaces";
        var response = await httpClient.AsCustomer(customerId.Value).PostAsync(postUrl, content);
        var apiSpace = await response.ReadAsHydraResponseAsync<Space>();
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be($"{postUrl}/{expectedSpace}");
        apiSpace.Should().NotBeNull();
        apiSpace.Name.Should().Be("Test Space");
        apiSpace.MaxUnauthorised.Should().Be(-1);
    }
    
    [Fact]
    public async Task Post_ComplexSpace_Creates_Space()
    {
        // arrange
        int? customerId = 99; //  await EnsureCustomerForSpaceTests("Post_ComplexSpace_Creates_Space");
        
        const string newSpaceJson = @"{
  ""@type"": ""Space"",
  ""name"": ""Test Complex Space"",
  ""defaultRoles"": [""role1"", ""role2""],
  ""defaultTags"": [""tag1"", ""tag2""],
  ""maxUnauthorised"": 400
}";
        // act
        var content = new StringContent(newSpaceJson, Encoding.UTF8, "application/json");
        var postUrl = $"/customers/{customerId}/spaces";
        var response = await httpClient.AsCustomer(customerId.Value).PostAsync(postUrl, content);
        var apiSpace = await response.ReadAsHydraResponseAsync<Space>();
        
        // assert
        apiSpace.Should().NotBeNull();
        AssertSpace(apiSpace);

        // verify that we can re-obtain the space with GET
        var newResponse = await httpClient.AsCustomer(customerId.Value).GetAsync(apiSpace.Id);
        var reObtainedSpace = await newResponse.ReadAsHydraResponseAsync<Space>();
        AssertSpace(reObtainedSpace);

        void AssertSpace(Space space)
        {
            space.Name.Should().Be("Test Complex Space");
            space.DefaultRoles.Should().BeEquivalentTo("role1", "role2");
            space.DefaultTags.Should().BeEquivalentTo("tag1", "tag2");
            space.MaxUnauthorised.Should().Be(400);
        }
    }


    [Fact]
    public async Task Create_Space_Updates_EntityCounters()
    {
        // After creating a space in Deliverator:
        
        // EXISTING COUNTER: was:
        // {'Type': 'space', 'Scope': '2', 'Next': 35, 'Customer': 2}
        // now:
        // {'Type': 'space', 'Scope': '2', 'Next': 36, 'Customer': 2}
    
        // NEW COUNTER
        // {'Type': 'space-images', 'Scope': '35', 'Next': 0, 'Customer': 2}
        
        // That last one seems wrong, should be Next = 1, not 0
        
        int? customerId = await EnsureCustomerForSpaceTests();

        var currentCounter = await dbContext.EntityCounters.SingleAsync(
            ec => ec.Type == "space" && ec.Scope == customerId.ToString() && ec.Customer == customerId);

        var next = (int)currentCounter.Next;
        const string newSpaceJson = @"{
  ""@type"": ""Space"",
  ""name"": ""Entity Counter Test Space""
}";
        
        var content = new StringContent(newSpaceJson, Encoding.UTF8, "application/json");
        var postUrl = $"/customers/{customerId}/spaces";
        var response = await httpClient.AsCustomer(customerId.Value).PostAsync(postUrl, content);
        var apiSpace = await response.ReadAsHydraResponseAsync<Space>();

        apiSpace.Id.Should().EndWith($"{postUrl}/{next}");
        currentCounter = await dbContext.EntityCounters.SingleAsync(
            ec => ec.Type == "space" && ec.Scope == customerId.ToString() && ec.Customer == customerId);
        currentCounter.Next.Should().Be(next + 1);
        var spaceImageCounter = await dbContext.EntityCounters.SingleOrDefaultAsync(
            ec => 
                ec.Type == "space-images" && 
                ec.Customer == customerId.Value && 
                ec.Scope == next.ToString());
        spaceImageCounter.Should().NotBeNull();
        spaceImageCounter.Next.Should().Be(1); // Deliverator makes 0 here. But that doesn't feel right!
    }
    
    // Run the comparer to look at new entity counters that appear when deliverator makes a space
    // full test creates customer, creates space, validates space, patches space
    // should these tests be cumulative?
    // (each test calls the previous tests)
    
    // https://github.com/dlcs/protagonist/issues/151

    // create, patch, delete - delete space? What if has images?
    
    
    
    // paging tests
    // create lots of spaces!
    
    [Fact]
    public async Task GetSpaces_Returns_HydraCollection()
    {
        // arrange
        int? customerId = await EnsureCustomerForSpaceTests("hydracollection_space");
        await EnsureSpaces(customerId.Value, 10);
        var spacesUrl = $"/customers/{customerId.Value}/spaces";
        
        // act
        var response = await httpClient.AsCustomer(customerId.Value).GetAsync(spacesUrl);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Should().NotBeNull();
        coll.Type.Should().Be("Collection");
        coll.Members.Should().HaveCount(10);
        coll.Members.Should().Contain(jo => jo["@id"].Value<string>().EndsWith($"{spacesUrl}/1"));
        coll.Members.Should().Contain(jo => jo["@id"].Value<string>().EndsWith($"{spacesUrl}/10"));
    }

    [Fact]
    public async Task Paged_Requests_Return_Correct_Views()
    {
        // arrange
        int? customerId = await EnsureCustomerForSpaceTests("hydracollection_space");
        await EnsureSpaces(customerId.Value, 55);
        // set a pageSize of 10
        var spacesUrl = $"/customers/{customerId.Value}/spaces?pageSize=10";
        
        // act
        var response = await httpClient.AsCustomer(customerId.Value).GetAsync(spacesUrl);
        
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
        coll.View.TotalPages.Should().Be(6);
        int pageCounter = 1;
        var view = coll.View;
        while (view.Next != null)
        {
            var nextResp = await httpClient.AsCustomer(customerId.Value).GetAsync(view.Next);
            var nextColl = await nextResp.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
            view = nextColl.View;
            view.Previous.Should().Contain("page=" + pageCounter);
            pageCounter++;
            if (pageCounter < 6)
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
    public async Task Paged_Requests_Support_Ordering()
    {
        // arrange
        int? customerId = await EnsureCustomerForSpaceTests("hydracollection_space");
        await EnsureSpaces(customerId.Value, 25);
        
        var spacesUrl = $"/customers/{customerId.Value}/spaces?pageSize=10&orderBy=name";
        
        // act
        var response = await httpClient.AsCustomer(customerId.Value).GetAsync(spacesUrl);
        
        // assert
        var coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members[0]["name"].ToString().Should().Be("Space 0001");
        coll.Members[1]["name"].ToString().Should().Be("Space 0002");
        
        spacesUrl = $"/customers/{customerId.Value}/spaces?pageSize=10&orderBy=name&ascending=false";
        response = await httpClient.AsCustomer(customerId.Value).GetAsync(spacesUrl);
        coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members[0]["name"].ToString().Should().Be("Space 0025");
        coll.Members[1]["name"].ToString().Should().Be("Space 0024");
        
        var nextPage = await httpClient.AsCustomer(customerId.Value).GetAsync(coll.View.Next);
        coll = await nextPage.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members[0]["name"].ToString().Should().Be("Space 0015");
        coll.Members[1]["name"].ToString().Should().Be("Space 0014");
        
        // Add another space just to be sure we are testing created properly
        const string newSpaceJson = @"{
  ""@type"": ""Space"",
  ""name"": ""Aardvark space""
}";
        var content = new StringContent(newSpaceJson, Encoding.UTF8, "application/json");
        var postUrl = $"/customers/{customerId}/spaces";
        await httpClient.AsCustomer(customerId.Value).PostAsync(postUrl, content);
        
        spacesUrl = $"/customers/{customerId.Value}/spaces?pageSize=10&orderBy=name";
        response = await httpClient.AsCustomer(customerId.Value).GetAsync(spacesUrl);
        coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members[0]["name"].ToString().Should().Be("Aardvark space");
        
        spacesUrl = $"/customers/{customerId.Value}/spaces?pageSize=10&orderBy=created";
        response = await httpClient.AsCustomer(customerId.Value).GetAsync(spacesUrl);
        coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members[0]["name"].ToString().Should().Be("Space 0001");
        
        spacesUrl = $"/customers/{customerId.Value}/spaces?pageSize=10&orderBy=created&ascending=false";
        response = await httpClient.AsCustomer(customerId.Value).GetAsync(spacesUrl);
        coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members[0]["name"].ToString().Should().Be("Aardvark space");

    }

    [Fact]
    public async Task Patch_Space_Updates_Name()
    {
        int? customerId = await EnsureCustomerForSpaceTests("Patch_Space_Updates_Name");
        await dbContext.Spaces.AddTestSpace(customerId.Value, 1, "Patch Space Before");
        await dbContext.SaveChangesAsync();
        
        const string patchJson = @"{
""@type"": ""Space"",
""name"": ""Patch Space After""
}";
        var patchContent = new StringContent(patchJson, Encoding.UTF8, "application/json");
        var patchUrl = $"/customers/{customerId}/spaces/1";
        var patchResponse = await httpClient.AsCustomer(customerId.Value).PatchAsync(patchUrl, patchContent);
        var patchedSpace = await patchResponse.ReadAsHydraResponseAsync<Space>();

        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        patchedSpace.Name.Should().Be("Patch Space After");
    }
    
    [Fact]
    public async Task Patch_Space_Prevents_Name_Conflict()
    {
        int? customerId = await EnsureCustomerForSpaceTests("Patch_Space_Prevents_Name_Conflict");
        await dbContext.Spaces.AddTestSpace(customerId.Value, 1, "Patch Space Name 1");
        await dbContext.Spaces.AddTestSpace(customerId.Value, 2, "Patch Space Name 2");
        await dbContext.SaveChangesAsync();
        
        const string patchJson = @"{
""@type"": ""Space"",
""name"": ""Patch Space Name 2""
}";
        var patchContent = new StringContent(patchJson, Encoding.UTF8, "application/json");
        var patchUrl = $"/customers/{customerId}/spaces/1";
        var patchResponse = await httpClient.AsCustomer(customerId.Value).PatchAsync(patchUrl, patchContent);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task Patch_Space_Leaves_Omitted_Fields_Intact()
    {
        // arrange
        int? customerId = await EnsureCustomerForSpaceTests("Patch_Space_Leaves_Omitted_Fields_Intact");
        
        const string newSpaceJson = @"{
          ""@type"": ""Space"",
          ""name"": ""Patch Complex Space"",
          ""defaultRoles"": [""role1"", ""role2""],
          ""defaultTags"": [""tag1"", ""tag2""],
          ""maxUnauthorised"": 400
        }";
        const string patchJson = @"{
          ""@type"": ""Space"",
          ""name"": ""Patch Complex Space After""
        }";
        
        // act
        var content = new StringContent(newSpaceJson, Encoding.UTF8, "application/json");
        var postUrl = $"/customers/{customerId}/spaces";
        var response = await httpClient.AsCustomer(customerId.Value).PostAsync(postUrl, content);
        var apiSpace = await response.ReadAsHydraResponseAsync<Space>();
        
        // assert
        var patchContent = new StringContent(patchJson, Encoding.UTF8, "application/json");
        var patchResponse = await httpClient.AsCustomer(customerId.Value).PatchAsync(apiSpace.Id, patchContent);
        var patchedSpace = await patchResponse.ReadAsHydraResponseAsync<Space>();

        patchedSpace.Name.Should().Be("Patch Complex Space After");
        patchedSpace.DefaultRoles.Should().BeEquivalentTo("role1", "role2");
        patchedSpace.DefaultTags.Should().BeEquivalentTo("tag1", "tag2");
        patchedSpace.MaxUnauthorised.Should().Be(400);
    }
    
    private async Task<int?> EnsureCustomerForSpaceTests(string customerName = "space-test-customer")
    {
        var spaceTestCustomer = await dbContext.Customers.SingleOrDefaultAsync(c => c.Name == customerName);

        if (spaceTestCustomer != null)
        {
            return spaceTestCustomer.Id;
        }
        
        string spaceTestCustomerJson = $@"{{
  ""@type"": ""Customer"",
  ""name"": ""{customerName}"",
  ""displayName"": ""Display - {customerName}""
}}";
        
        var content = new StringContent(spaceTestCustomerJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsAdmin().PostAsync("/customers", content);
        var apiCustomer = await response.ReadAsHydraResponseAsync<Customer>();
        return apiCustomer?.Id.GetLastPathElementAsInt();
    }

    /// <summary>
    /// Create lots of spaces to test paging 
    /// </summary>
    /// <param name="numberOfSpaces"></param>
    private async Task EnsureSpaces(int customerId, int numberOfSpaces)
    {
        var seed = DateTime.Now.Ticks.ToString();
        const string newSpaceJsonTemplate = @"{
  ""@type"": ""Space"",
  ""name"": ""{space-name}""
}";
        var postUrl = $"/customers/{customerId}/spaces";
        
        for (int i = 1; i <= numberOfSpaces; i++)
        {
            var spaceName = $"Space {i.ToString().PadLeft(4, '0')}";
            var newSpaceJson = newSpaceJsonTemplate.Replace("{space-name}", spaceName);
            
            var content = new StringContent(newSpaceJson, Encoding.UTF8, "application/json");
            var response = await httpClient.AsCustomer(customerId).PostAsync(postUrl, content);
            var apiSpace = await response.ReadAsHydraResponseAsync<Space>();
            apiSpace.Name.Should().Be(spaceName);
        }

    }

}