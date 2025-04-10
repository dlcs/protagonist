using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Repository;
using Hydra.Collections;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class CustomerImageTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;

    public CustomerImageTests(StorageFixture storageFixture, ProtagonistAppFactory<Startup> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(storageFixture.DbFixture, "API-Test",
            f => f.WithLocalStack(storageFixture.LocalStackFixture));
        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task Post_AllImages_400_IfInvalidFormatId()
    {
        // Arrange
        const string allImagesJson = @"{
  ""@type"": ""Collection"",
  ""member"": [
    { ""id"": ""1/not-an-id"" },
    ]
}";
        var content = new StringContent(allImagesJson, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PostAsync("/customers/99/allImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_AllImages_400_IfRequestDifferentCustomer()
    {
        // Arrange
        const string allImagesJson = @"{
  ""@type"": ""Collection"",
  ""member"": [ { ""id"": ""1/1/diff-customer"" } ]
}";
        var content = new StringContent(allImagesJson, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PostAsync("/customers/99/allImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_AllImages_200_IfNoMatches()
    {
        // Arrange
        const string allImagesJson = @"{
  ""@type"": ""Collection"",
  ""member"": [ { ""id"": ""99/1/not-found"" }, { ""id"": ""99/1/not-found-2"" } ]
}";
        var content = new StringContent(allImagesJson, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PostAsync("/customers/99/allImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>();
        collection.Members.Should().BeEmpty();
        collection.PageSize.Should().Be(0);
        collection.TotalItems.Should().Be(0);
    }
    
    [Fact]
    public async Task Post_AllImages_200_WithMatches()
    {
        // Arrange
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/1/allImages_1"));
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/1/allImages_2"));
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/2/allImages_3"), space: 2);
        await dbContext.SaveChangesAsync();
        
        const string newCustomerJson = @"{
  ""@type"": ""Collection"",
  ""member"": [ 
    { ""id"": ""99/1/allImages_1"" }, { ""id"": ""99/1/allImages_2"" },
    { ""id"": ""99/2/allImages_3"" }, { ""id"": ""99/1/not-found"" }
 ]
}";
        var content = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PostAsync("/customers/99/allImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>();
        collection.Members.Should().HaveCount(3);
        collection.PageSize.Should().Be(3);
        collection.TotalItems.Should().Be(3);
    }
    
    [Fact]
    public async Task Post_DeleteImages_400_IfInvalidFormatId()
    {
        // Arrange
        const string allImagesJson = @"{
  ""@type"": ""Collection"",
  ""member"": [
    { ""id"": ""1/not-an-id"" },
    ]
}";
        var content = new StringContent(allImagesJson, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PostAsync("/customers/99/deleteImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_DeleteImages_400_IfRequestDifferentCustomer()
    {
        // Arrange
        const string allImagesJson = @"{
  ""@type"": ""Collection"",
  ""member"": [ { ""id"": ""1/1/diff-customer"" } ]
}";
        var content = new StringContent(allImagesJson, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PostAsync("/customers/99/deleteImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_DeleteImages_400_IfNoMatches()
    {
        // Arrange
        const string allImagesJson = @"{
  ""@type"": ""Collection"",
  ""member"": [ { ""id"": ""99/1/not-found"" }, { ""id"": ""99/1/not-found-2"" } ]
}";
        var content = new StringContent(allImagesJson, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PostAsync("/customers/99/deleteImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_DeleteImages_200_WithMatches()
    {
        // Arrange
        var reference = nameof(Post_DeleteImages_200_WithMatches);
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/1/deleteImages_1"), ref1: reference);
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/1/deleteImages_2"), ref1: reference);
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/2/deleteImages_3"), space: 2, ref1: reference);
        await dbContext.SaveChangesAsync();
        
        const string newCustomerJson = @"{
  ""@type"": ""Collection"",
  ""member"": [ 
    { ""id"": ""99/1/deleteImages_1"" }, { ""id"": ""99/1/deleteImages_2"" },
    { ""id"": ""99/2/deleteImages_3"" }, { ""id"": ""99/1/not-found"" }
 ]
}";
        var content = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PostAsync("/customers/99/deleteImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dbContext.Images.Count(i => i.Reference1 == reference).Should().Be(0);
    }
}