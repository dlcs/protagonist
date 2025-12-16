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
using Hydra.Model;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class AdjunctTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dbContext;
    
    public AdjunctTests(StorageFixture storageFixture, ProtagonistAppFactory<Startup> factory)
    {
        var dbFixture = storageFixture.DbFixture;
        
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
        
        const string newAdjunctJson = $@"{{
          ""id"": ""someAdjunctId"",
          ""externalId"": ""https://some-location.com/an-adjunct"",
          ""iiifLink"": ""SeeAlso"",
          ""mediaType"": ""a-mediaType"",
          ""label"": {{""label"": [""value""]}},
          ""language"": [""en""],
        }}";
        
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
        adjunct.IIIFLink.Should().Be("SeeAlso");
        adjunct.Label.First().Key.Should().Be("label");
        adjunct.Language.Should().Contain(l => l == "en").And.HaveCount(1);
        adjunct.AssetId.Should().BeNull();
        adjunct.ExternalId.Should().Be("https://some-location.com/an-adjunct");
    }
    
    [Fact]
    public async Task PostAdjunct_FailsToCreateExternalAdjunct_FailsValidation()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = $@"{{
          ""id"": ""someAdjunctId"",
          ""externalId"": ""an-adjunct"",
          ""iiifLink"": ""SeeAlso"",
          ""mediaType"": ""a-mediaType"",
          ""label"": {{""label"": [""value""]}},
          ""language"": [""en""],
        }}";
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        error.Detail.Should().Be("'externalId' must be a well formed URI");
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
          ""externalId"": ""https://some-location.com/an-adjunct"",
          ""iiifLink"": ""SeeAlso"",
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
        error.Detail.Should().Be("An adjunct called 'someAdjunctId' already exists");
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
        adjunct.IIIFLink.Should().Be("SeeAlso");
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
    public async Task PutAdjunct_FailsToCreateAdjunct_WhenIdDiffersBetweenBodyAndUri()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = $@"{{
          ""id"": ""someAdjunctId"",
          ""externalId"": ""https://some-location.com/an-adjunct"",
          ""iiifLink"": ""SeeAlso"",
          ""mediaType"": ""a-mediaType"",
          ""label"": {{""label"": [""value""]}},
          ""language"": [""en""],
        }}";
        
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

        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.Adjuncts.AddTestAdjunct("someAdjunctId", assetId, created: DateTime.UtcNow.AddDays(-2),
            modified: DateTime.UtcNow.AddDays(-2));
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = $@"{{
          ""id"": ""someAdjunctId"",
          ""externalId"": ""https://some-location.com/an-adjunct"",
          ""iiifLink"": ""SeeAlso"",
          ""mediaType"": ""a-mediaType"",
          ""label"": {{""label"": [""value""]}},
          ""language"": [""en""],
        }}";
        
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
        adjunct.IIIFLink.Should().Be("SeeAlso");
        adjunct.Label.First().Key.Should().Be("label");
        adjunct.Language.Should().Contain(l => l == "en").And.HaveCount(1);
        adjunct.AssetId.Should().BeNull();
        adjunct.Created.Should().BeCloseTo(DateTime.UtcNow.AddDays(-2), TimeSpan.FromSeconds(5));
        adjunct.Modified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.ExternalId.Should().Be("https://some-location.com/an-adjunct");
    }
    
    [Fact]
    public async Task PutAdjunct_CreatesAdjunct_WhenIdDoesNotExists()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = $@"{{
          ""id"": ""someAdjunctId"",
          ""externalId"": ""https://some-location.com/an-adjunct"",
          ""iiifLink"": ""SeeAlso"",
          ""mediaType"": ""a-mediaType"",
          ""label"": {{""label"": [""value""]}},
          ""language"": [""en""],
        }}";
        
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
        adjunct.IIIFLink.Should().Be("SeeAlso");
        adjunct.Label.First().Key.Should().Be("label");
        adjunct.Language.Should().Contain(l => l == "en").And.HaveCount(1);
        adjunct.AssetId.Should().BeNull();
        adjunct.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.Modified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.ExternalId.Should().Be("https://some-location.com/an-adjunct");
    }

    // todo: @type
}
