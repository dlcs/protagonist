using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.HydraModel;
using DLCS.Repository;
using DLCS.Web.Response;
using Hydra.Collections;
using Hydra.Model;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class AdjunctTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dbContext;
    
    public AdjunctTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbContext = dbFixture.DbContext;
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task PostAdjunct_CreatesExternalAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = @"{
          ""id"": ""someAdjunctId"",
          ""@type"": ""Image"",
          ""externalId"": ""https://some-location.com/an-adjunct"",
          ""iiifLink"": ""seeAlso"",
          ""mediaType"": ""a-mediaType"",
          ""label"": {""label"": [""value""]},
          ""language"": [""en""],
        }";
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var adjunct = await response.ReadAsHydraResponseAsync<Adjunct>();

        adjunct.Id.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId");
        adjunct.IIIFLink.Should().Be("seeAlso");
        adjunct.Label.First().Key.Should().Be("label");
        adjunct.Language.Should().Contain(l => l == "en").And.HaveCount(1);
        adjunct.AssetId.Should().BeNull();
        adjunct.ExternalId.Should().Be("https://some-location.com/an-adjunct");
        adjunct.PublicId.Should().Be("https://some-location.com/an-adjunct");
        
        response.Headers.Location.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId");
    }
    
    [Fact]
    public async Task PostAdjunct_FailsToCreateExternalAdjunct_FailsValidation()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = @"{
          ""id"": ""someAdjunctId"",
          ""@type"": ""Image"",
          ""externalId"": ""an-adjunct"",
          ""iiifLink"": ""seeAlso"",
          ""mediaType"": ""a-mediaType"",
          ""label"": {""label"": [""value""]},
          ""language"": [""en""],
        }";
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        error.Detail.Should().Be("'externalId' is required and must be a well formed URI");
    }
    
    [Fact]
    public async Task PostAdjunct_FailsToCreateAdjunct_WhenIdAlreadyExists()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.Adjuncts.AddTestAdjunct("someAdjunctId", assetId);
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = $@"{{
          ""id"": ""someAdjunctId"",
          ""@type"": ""Image"",
          ""externalId"": ""https://some-location.com/an-adjunct"",
          ""iiifLink"": ""seeAlso"",
          ""mediaType"": ""a-mediaType"",
          ""label"": {{""label"": [""value""]}},
          ""language"": [""en""],
        }}";
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Detail.Should().Be("An adjunct with id 'someAdjunctId' already exists");
    }
    
    [Fact]
    public async Task GetAdjunct_RetrievesAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.Adjuncts.AddTestAdjunct("someAdjunctId", assetId);
        await dbContext.SaveChangesAsync();
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/someAdjunctId";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var adjunct = await response.ReadAsHydraResponseAsync<Adjunct>();

        adjunct.Id.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId");
        adjunct.IIIFLink.Should().Be("seeAlso");
    }
    
    [Fact]
    public async Task GetAdjunct_NotFound_WhenNoAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();

        var path = $"{assetId.ToApiResourcePath()}/adjuncts/someAdjunctId";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task GetAdjunct_NotFound_WhenNoAsset()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        var path = $"{assetId.ToApiResourcePath()}/adjuncts/someAdjunctId";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task GetAllAdjuncts_RetrievesListOfAdjuncts_OrderedById()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.Adjuncts.AddTestAdjunct("someAdjunctId2", assetId);
        await dbContext.Adjuncts.AddTestAdjunct("someAdjunctId", assetId);
        await dbContext.Adjuncts.AddTestAdjunct("someAdjunctId3", assetId);
        await dbContext.SaveChangesAsync();
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var adjunct = await response.ReadAsHydraResponseAsync<HydraCollection<Adjunct>>();

        adjunct.Members.Length.Should().Be(3);
        adjunct.Members[0].Id.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId");
        adjunct.Members[1].Id.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId2");
        adjunct.Members[2].Id.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId3");
    }
    
    [Fact]
    public async Task GetAllAdjuncts_RetrievesEmptyListOfAdjuncts_WhenNoAdjuncts()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var adjunct = await response.ReadAsHydraResponseAsync<HydraCollection<Adjunct>>();

        adjunct.Members.Length.Should().Be(0);
    }
    
    [Fact]
    public async Task GetAllAdjuncts_Returns404_WhenNoAsset()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task PutAdjunct_FailsToCreateAdjunct_WhenIdDiffersBetweenBodyAndUri()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = @"{
          ""id"": ""someAdjunctId"",
          ""@type"": ""Image"",
          ""externalId"": ""https://some-location.com/an-adjunct"",
          ""iiifLink"": ""seeAlso"",
          ""mediaType"": ""a-mediaType"",
          ""label"": {""label"": [""value""]},
          ""language"": [""en""],
        }";
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/differentId";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Detail.Should().Be("The adjunct id from the request URI does not match the 'id' from the request body");
    }
    
    [Fact]
    public async Task PutAdjunct_UpdatesAdjunct_WhenIdAlreadyExists()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Adjuncts.AddTestAdjunct("someAdjunctId", assetId, created: DateTime.UtcNow.AddDays(-2));
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = @"{
          ""id"": ""someAdjunctId"",
          ""@type"": ""Image"",
          ""externalId"": ""https://some-location.com/an-adjunct"",
          ""iiifLink"": ""seeAlso"",
          ""mediaType"": ""a-mediaType"",
          ""label"": {""label"": [""value""]},
          ""language"": [""en""],
        }";
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/someAdjunctId";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var adjunct = await response.ReadAsHydraResponseAsync<Adjunct>();

        adjunct.Id.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId");
        adjunct.IIIFLink.Should().Be("seeAlso");
        adjunct.Label.First().Key.Should().Be("label");
        adjunct.Language.Should().Contain(l => l == "en").And.HaveCount(1);
        adjunct.AssetId.Should().BeNull();
        adjunct.Created.Should().BeCloseTo(DateTime.UtcNow.AddDays(-2), TimeSpan.FromSeconds(5));
        adjunct.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.ExternalId.Should().Be("https://some-location.com/an-adjunct");
        adjunct.PublicId.Should().Be("https://some-location.com/an-adjunct");
    }
    
    [Fact]
    public async Task PutAdjunct_CreatesAdjunct_WhenIdDoesNotExists()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = @"{
          ""id"": ""someAdjunctId"",
          ""@type"": ""Image"",
          ""externalId"": ""https://some-location.com/an-adjunct"",
          ""iiifLink"": ""seeAlso"",
          ""mediaType"": ""a-mediaType"",
          ""label"": {""label"": [""value""]},
          ""language"": [""en""],
        }";
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/someAdjunctId";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var adjunct = await response.ReadAsHydraResponseAsync<Adjunct>();

        adjunct.Id.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId");
        adjunct.IIIFLink.Should().Be("seeAlso");
        adjunct.Label.First().Key.Should().Be("label");
        adjunct.Language.Should().Contain(l => l == "en").And.HaveCount(1);
        adjunct.AssetId.Should().BeNull();
        adjunct.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.ExternalId.Should().Be("https://some-location.com/an-adjunct");
        adjunct.PublicId.Should().Be("https://some-location.com/an-adjunct");
        
        response.Headers.Location.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId");
    }
    
    [Fact]
    public async Task DeleteAdjunct_Returns404_WhenDoesNotExist()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/someAdjunctId";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).DeleteAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task DeleteAdjunct_DeletesAdjunct_WhenDoesExist()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.Adjuncts.AddTestAdjunct("someAdjunctId", assetId);
        await dbContext.SaveChangesAsync();
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/someAdjunctId";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).DeleteAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
