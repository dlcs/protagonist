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
[Collection(StorageCollection.CollectionName)]
public class AdjunctTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dbContext;
    
    public AdjunctTests(StorageFixture storageFixture, ProtagonistAppFactory<Startup> factory)
    {
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(storageFixture.DbFixture, "API-Test",
            f => f.WithLocalStack(storageFixture.LocalStackFixture));
        dbContext = storageFixture.DbFixture.DbContext;
        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task PostAdjunct_CreatesExternalAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = """
                                      {
                                                "id": "someAdjunctId",
                                                "@type": "Image",
                                                "externalId": "https://some-location.com/an-adjunct",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                              }
                                      """;
        
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
        adjunct.ExternalId.Should().Be("https://some-location.com/an-adjunct");
        adjunct.PublicId.Should().Be("https://some-location.com/an-adjunct");
        
        response.Headers.Location.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId");
    }
    
    [Fact]
    public async Task PostAdjunct_Returns400_FailsValidation()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = """
                                      {
                                                "id": "someAdjunctId",
                                                "@type": "Image",
                                                "externalId": "an-adjunct",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                              }
                                      """;
        
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
    public async Task PostAdjunct_Returns400_IfIdMissing()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = """
                                      {
                                                "@type": "Image",
                                                "externalId": "https://example.com/adjunct",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        error.Detail.Should().Be("Adjunct identifier could not be found");
    }
    
    [Fact]
    public async Task PostAdjunct_Returns400_WhenIdInvalid()
    {
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        var newAdjunctJson = """
                             {
                                       "id": "model Id",
                                       "@type": "Image",
                                       "externalId": "https://some-location.com/an-adjunct",
                                       "iiifLink": "seeAlso",
                                       "mediaType": "a-mediaType",
                                       "label": {"label": ["value"]},
                                       "language": ["en"],
                                     }
                             """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task PostAdjunct_Returns409_WhenIdAlreadyExists()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct("someAdjunctId");
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = """
                                      {
                                                "id": "someAdjunctId",
                                                "@type": "Image",
                                                "externalId": "https://some-location.com/an-adjunct",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Detail.Should().Be("Create failed. An adjunct with id 'someAdjunctId' already exists");
    }
    
    [Fact]
    public async Task PostAdjunct_Returns409_WhenIdAlreadyExists_DifferentCase()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "someAdjunctId";

        await dbContext.Images.AddTestAsset(assetId) 
            .WithTestAdjunct(adjunctId.ToUpper());
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = $$"""
                                        {
                                                  "id": "{{adjunctId}}",
                                                  "@type": "Image",
                                                  "externalId": "https://some-location.com/an-adjunct",
                                                  "iiifLink": "seeAlso",
                                                  "mediaType": "a-mediaType",
                                                  "label": {"label": ["value"]},
                                                  "language": ["en"],
                                                }
                                        """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Detail.Should().Be("Create failed. An adjunct with id 'someAdjunctId' already exists");
    }
    
    [Fact]
    public async Task PostAdjunct_Returns404_WhenAssetDoesNotExist()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        
        const string newAdjunctJson = """
                                      {
                                                "id": "adj123",
                                                "@type": "Image",
                                                "externalId": "https://some-location.com/an-adjunct",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Detail.Should().Be($"Asset with id '{assetId}' not found");
    }
    
    [Fact]
    public async Task GetAdjunct_RetrievesAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct("someAdjunctId");
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
    public async Task GetAdjunct_NotFound_WhenAdjunctDiffersByCase()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "bonobo";

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId.ToUpper());
        await dbContext.SaveChangesAsync();

        var path = $"{assetId.ToApiResourcePath()}/adjuncts/{adjunctId}";

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

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct("someAdjunctId2")
            .WithTestAdjunct("someAdjunctId")
            .WithTestAdjunct("someAdjunctId3");
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
    public async Task PutAdjunct_Returns404_WhenAssetDoesNotExist()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        
        const string newAdjunctJson = """
                                      {
                                                "@type": "Image",
                                                "externalId": "https://some-location.com/an-adjunct",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/foo";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Detail.Should().Be($"Asset with id '{assetId}' not found");
    }
    
    [Fact]
    public async Task PutAdjunct_Returns400_WhenIdDiffersBetweenBodyAndUri()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = """
                                      {
                                                "id": "someAdjunctId",
                                                "@type": "Image",
                                                "externalId": "https://some-location.com/an-adjunct",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/differentId";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Detail.Should()
            .Be(
                "The adjunct id from the request URI (differentId) does not match the 'id' from the request body (someAdjunctId)");
    }

    [Fact]
    public async Task PutAdjunct_Returns400_WhenIdInvalid()
    {
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        var newAdjunctJson = """
                             {
                                       "id": "model Id",
                                       "@type": "Image",
                                       "externalId": "https://some-location.com/an-adjunct",
                                       "iiifLink": "seeAlso",
                                       "mediaType": "a-mediaType",
                                       "label": {"label": ["value"]},
                                       "language": ["en"],
                                     }
                             """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/differentId";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutAdjunct_Returns409_WhenIdAlreadyExists_DifferentCase()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "someAdjunctId";

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId.ToUpper());
        await dbContext.SaveChangesAsync();
        
        const string updateAdjunctJson = """
                                         {
                                                   "@type": "Image",
                                                   "externalId": "https://some-location.com/an-adjunct",
                                                   "iiifLink": "seeAlso",
                                                   "mediaType": "a-mediaType",
                                                   "label": {"label": ["value"]},
                                                   "language": ["en"],
                                                 }
                                         """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/{adjunctId}";
        var content = new StringContent(updateAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Detail.Should().Be($"Create failed. An adjunct with id '{adjunctId}' already exists");
    }
    
    [Fact]
    public async Task PutAdjunct_UpdatesAdjunct_WhenIdAlreadyExists()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct("someAdjunctId", created: DateTime.UtcNow.AddDays(-2));
        await dbContext.SaveChangesAsync();
        
        const string updateAdjunctJson = """
                                         {
                                                   "id": "someAdjunctId",
                                                   "@type": "Image",
                                                   "externalId": "https://some-location.com/an-adjunct",
                                                   "iiifLink": "seeAlso",
                                                   "mediaType": "a-mediaType",
                                                   "label": {"label": ["value"]},
                                                   "language": ["en"],
                                                 }
                                         """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/someAdjunctId";
        var content = new StringContent(updateAdjunctJson, Encoding.UTF8, "application/json");

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
        const string adjunctId = "someAdjunctId";

        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = """
                                      {
                                                "id": "someAdjunctId",
                                                "@type": "Image",
                                                "externalId": "https://some-location.com/an-adjunct",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                                "size": 1234,
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/someAdjunctId";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var adjunct = await response.ReadAsHydraResponseAsync<Adjunct>();

        var expectedAdjunctId = $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/{adjunctId}";
        adjunct.Id.Should().Be(expectedAdjunctId);
        adjunct.IIIFLink.Should().Be("seeAlso");
        adjunct.Label.First().Key.Should().Be("label");
        adjunct.Language.Should().Contain(l => l == "en").And.HaveCount(1);
        adjunct.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.ExternalId.Should().Be("https://some-location.com/an-adjunct");
        adjunct.PublicId.Should().Be("https://some-location.com/an-adjunct");
        adjunct.Size.Should().Be(1234);
        
        response.Headers.Location.Should().Be(expectedAdjunctId);
        
        dbContext.Adjuncts.Any(a => a.AssetId == assetId && a.Id == adjunctId).Should()
            .BeTrue("Adjunct persisted to DB");
    }
    
    [Fact]
    public async Task PutAdjunct_CreatesAdjunct_UsingIdFromUri_IfBodyIdMissing()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var adjunctId = assetId.Asset;

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = """
                                      {
                                                "@type": "AnnotationPage",
                                                "externalId": "https://some-location.com/an-adjunct",
                                                "iiifLink": "annotations",
                                                "mediaType": "application/json",
                                                "label": {"none": ["value"]}
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/{adjunctId}";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var adjunct = await response.ReadAsHydraResponseAsync<Adjunct>();
        
        var expectedAdjunctId = $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/{adjunctId}";
        
        adjunct.Id.Should().Be(expectedAdjunctId);
        adjunct.IIIFLink.Should().Be("annotations");
        adjunct.Label.First().Key.Should().Be("none");
        adjunct.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.ExternalId.Should().Be("https://some-location.com/an-adjunct");
        adjunct.PublicId.Should().Be("https://some-location.com/an-adjunct");
        
        response.Headers.Location.Should().Be(expectedAdjunctId);

        dbContext.Adjuncts.Any(a => a.AssetId == assetId && a.Id == adjunctId).Should()
            .BeTrue("Adjunct persisted to DB");
    }
    
    [Fact]
    public async Task PutAdjunct_CreatesAdjunct_UsingIdFromUri_IfBodyIdEmpty()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var adjunctId = assetId.Asset;

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = """
                                      {
                                                "id": "",           
                                                "@type": "AnnotationPage",
                                                "externalId": "https://some-location.com/an-adjunct",
                                                "iiifLink": "annotations",
                                                "mediaType": "application/json",
                                                "label": {"none": ["value"]}
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/{adjunctId}";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var adjunct = await response.ReadAsHydraResponseAsync<Adjunct>();
        
        adjunct.Id.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/{adjunctId}");
        adjunct.IIIFLink.Should().Be("annotations");
        adjunct.Label.First().Key.Should().Be("none");
        adjunct.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.ExternalId.Should().Be("https://some-location.com/an-adjunct");
        adjunct.PublicId.Should().Be("https://some-location.com/an-adjunct");
        
        dbContext.Adjuncts.Any(a => a.AssetId == assetId && a.Id == adjunctId).Should().BeTrue("Adjunct persisted to DB");
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

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct("someAdjunctId");
        await dbContext.SaveChangesAsync();
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts/someAdjunctId";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).DeleteAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
    
    [Fact]
    public async Task PostAdjunct_CreatesHostedAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = """
                                      {
                                                "id": "someAdjunctId",
                                                "@type": "Image",
                                                "origin": "https://some-location.com/an-adjunct",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                              }
                                      """;
        
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
        adjunct.Origin.Should().Be("https://some-location.com/an-adjunct");
        adjunct.PublicId.Should().Be("https://dlcs.digirati.io/adjuncts/99/1/PostAdjunct_CreatesHostedAdjunct/someAdjunctId");
        
        response.Headers.Location.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId");
    }
}
