using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Model.Page;
using DLCS.Repository;
using DLCS.Web.Response;
using Hydra.Collections;
using Hydra.Model;
using Test.Helpers.Data;
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
    
    [Theory]
    [InlineData("first", "[\"second\"]", "add", "first", "second")]
    [InlineData("first", "[\"second\",\"third\"]", "add", "first", "second", "third")]
    [InlineData(null, "[\"first\"]", "add", "first")]
    [InlineData("first", "[\"second\"]", "replace", "second")]
    [InlineData(null, "[\"first\"]", "replace", "first")]
    [InlineData("first", "[\"first\",\"second\"]", "replace", "first", "second")]
    [InlineData("first,second", "[\"first\"]", "remove", "second")]
    [InlineData("first", "[\"second\"]", "remove", "first")]
    [InlineData("first,second,third", "[\"second\",\"third\"]", "remove", "first")]
    [InlineData("first,second", "[]", "remove", "first", "second")]
    public async Task Patch_AllImages_TestManifestPermutations(string initial, string update, string operation, params string[] result)
    {
        // Arrange
        var assetid = AssetIdGenerator.GetAssetId();
        var asset = await dbContext.Images.AddTestAsset(assetid, manifests: initial?.Split(',').ToList());
        await dbContext.SaveChangesAsync();

        var patchAllImages = $@"{{
  ""member"": [
    {{ ""id"": ""{assetid}"" }},
    ],
  ""field"": ""manifests"",
  ""value"": {update},
  ""operation"": ""{operation}""
}}";
        
        var content = new StringContent(patchAllImages, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PatchAsync("/customers/99/allImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>();
        collection.Members.Should().HaveCount(1);
        collection.Members[0].Manifests.Should().BeEquivalentTo(result);

        await asset.ReloadAsync();
        asset.Entity.Manifests.Should().BeEquivalentTo(result);
    }
    
    [Theory]
    [InlineData("first", "[\"second\"]", "add", "first", "second")]
    [InlineData("first", "[\"second\",\"third\"]", "add", "first", "second", "third")]
    [InlineData(null, "[\"first\"]", "add", "first")]
    [InlineData("first", "[\"second\"]", "replace", "second")]
    [InlineData(null, "[\"first\"]", "replace", "first")]
    [InlineData("first", "[\"first\",\"second\"]", "replace", "first", "second")]
    [InlineData("first,second", "[\"first\"]", "remove", "second")]
    [InlineData("first", "[\"second\"]", "remove", "first")]
    [InlineData("first,second,third", "[\"second\",\"third\"]", "remove", "first")]
    [InlineData("first,second", "[]", "remove", "first", "second")]
    public async Task Patch_AllImages_TestManifestPermutations_MultipleAssets(string initial, string update, string operation, params string[] result)
    {
        // Arrange
        var assetIdOne = AssetIdGenerator.GetAssetId(assetPostfix: "_1");
        var assetIdTwo = AssetIdGenerator.GetAssetId(assetPostfix: "_2");
        var assetOne = await dbContext.Images.AddTestAsset(assetIdOne, manifests: initial?.Split(',').ToList());
        var assetTwo = await dbContext.Images.AddTestAsset(assetIdTwo, manifests: initial?.Split(',').ToList());
        await dbContext.SaveChangesAsync();

        var patchAllImages = $@"{{
  ""member"": [
    {{ ""id"": ""{assetIdOne}"" }},
    {{ ""id"": ""{assetIdTwo}"" }}
    ],
  ""field"": ""manifests"",
  ""value"": {update},
  ""operation"": ""{operation}""
}}";
        
        var content = new StringContent(patchAllImages, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PatchAsync("/customers/99/allImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>();
        collection.Members.Should().HaveCount(2);
        collection.Members[0].Manifests.Should().BeEquivalentTo(result);
        collection.Members[1].Manifests.Should().BeEquivalentTo(result);

        await assetOne.ReloadAsync();
        assetOne.Entity.Manifests.Should().BeEquivalentTo(result);
        
        await assetTwo.ReloadAsync();
        assetTwo.Entity.Manifests.Should().BeEquivalentTo(result);
    }
    
    [Fact]
    public async Task Patch_AllImages_TestManifestRemoval()
    {
        // Arrange
        var assetid = AssetIdGenerator.GetAssetId();
        var asset = await dbContext.Images.AddTestAsset(assetid, manifests: ["first"]);
        await dbContext.SaveChangesAsync();

        var patchAllImages = $@"{{
  ""member"": [
    {{ ""id"": ""{assetid}"" }},
    ],
  ""field"": ""manifests"",
  ""value"": [],
  ""operation"": ""replace""
}}";
        
        var content = new StringContent(patchAllImages, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PatchAsync("/customers/99/allImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>();
        collection.Members.Should().HaveCount(1);
        collection.Members[0].Manifests.Should().BeNull();
        
        await asset.ReloadAsync();
        asset.Entity.Manifests.Should().BeNull();
    }
    
    [Fact]
    public async Task Patch_AllImages_TestManifestNotFound()
    {
        // Arrange
        var patchAllImages = $@"{{
  ""member"": [
    {{ ""id"": ""99/1/not-found"" }},
    ],
  ""field"": ""manifests"",
  ""value"": [""first""],
  ""operation"": ""replace""
}}";
        
        var content = new StringContent(patchAllImages, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PatchAsync("/customers/99/allImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>();
        collection.Members.Should().BeEmpty();
    }
    
    [Fact]
    public async Task Patch_AllImages_TestManifestMultiple()
    {
        // Arrange
        var assetIdOne = AssetIdGenerator.GetAssetId(assetPostfix: "_1");
        var assetIdTwo = AssetIdGenerator.GetAssetId(assetPostfix: "_2");
        await dbContext.Images.AddTestAsset(assetIdOne, manifests: ["first"]);
        await dbContext.Images.AddTestAsset(assetIdTwo, manifests: ["first"]);
        await dbContext.SaveChangesAsync();

        var patchAllImages = $@"{{
  ""member"": [
    {{ ""id"": ""{assetIdOne}"" }},
    {{ ""id"": ""{assetIdTwo}"" }}
    ],
  ""field"": ""manifests"",
  ""value"": [""second""],
  ""operation"": ""add""
}}";
        
        var content = new StringContent(patchAllImages, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PatchAsync("/customers/99/allImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>();
        collection.Members.Should().HaveCount(2);
        collection.Members[0].Manifests.Should().BeEquivalentTo("first", "second");
        collection.Members[1].Manifests.Should().BeEquivalentTo("first", "second");
    }
    
    [Fact]
    public async Task Patch_AllImages_BadRequest_WhenValueNotCorrect()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();;
        await dbContext.Images.AddTestAsset(assetId, manifests: ["first"]);
        await dbContext.SaveChangesAsync();

        var patchAllImages = $@"{{
  ""member"": [
    {{ ""id"": ""{assetId}"" }}
    ],
  ""field"": ""manifests"",
  ""value"": ""incorrect"",
  ""operation"": ""add""
}}";
        
        var content = new StringContent(patchAllImages, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PatchAsync("/customers/99/allImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Patch_AllImages_BadRequest_WhenFieldNotCorrect()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();;
        await dbContext.Images.AddTestAsset(assetId, manifests: ["first"]);
        await dbContext.SaveChangesAsync();

        var patchAllImages = $@"{{
  ""member"": [
    {{ ""id"": ""{assetId}"" }}
    ],
  ""field"": ""incorrect"",
  ""value"": [""first""],
  ""operation"": ""add""
}}";
        
        var content = new StringContent(patchAllImages, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PatchAsync("/customers/99/allImages", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.ReadAsJsonAsync<Error>(false);
        error.Detail.Should().Be("Unsupported field 'incorrect'");
    }
    
    [Fact]
    public async Task Get_AllImages_200_WithMatches()
    {
        // Arrange
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/1/allImages_1"));
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/1/allImages_2"));
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/2/allImages_3"), space: 2);
        await dbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer().GetAsync("/customers/99/allImages");

        // Assert
        var stuff = await response.Content.ReadAsStringAsync();
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var collection = await response.ReadAsHydraResponseAsync<PageOf<Image>>();
        collection.Page.Should().Be(1);
        collection.PageSize.Should().Be(3);
        collection.Entities.Should().HaveCount(3);
    }
}
