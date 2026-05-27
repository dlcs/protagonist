using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using API.Client;
using API.Infrastructure;
using API.Infrastructure.Messaging.General;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Messaging;
using DLCS.Repository;
using DLCS.Web.Response;
using FakeItEasy;
using Hydra.Collections;
using Hydra.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;
using Adjunct = DLCS.HydraModel.Adjunct;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class AdjunctTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dbContext;
    private static readonly IDeliverableNotificationSender DeliverableNotificationSender = A.Fake<IDeliverableNotificationSender>();
    private static readonly IIngestNotificationSender IngestNotificationSender = A.Fake<IIngestNotificationSender>();
    
    public AdjunctTests(StorageFixture storageFixture, ProtagonistAppFactory<Startup> factory)
    {
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient
        (
            storageFixture.DbFixture, "API-Test",
            f => f.WithLocalStack(storageFixture.LocalStackFixture
            ).WithTestServices(services =>
            {
                services.AddAuthentication("API-Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "API-Test", o => { o.TimeProvider = TimeProvider.System;  });
                services.AddSingleton(_ => DeliverableNotificationSender);
                services.AddScoped(_ => IngestNotificationSender);
            }));
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
                                                "@type": "AnnotationPage",
                                                "externalId": "https://some-location.com/an-adjunct",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "motivation": "a motivation",
                                                "provides": "translation",
                                                "language": ["en"],
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var hydra = await response.ReadAsHydraResponseAsync<HydraCollection<Adjunct>>();

        hydra.Should().NotBeNull();
        hydra.Members.Should().HaveCount(1);
        hydra.PageSize.Should().Be(1);
        hydra.TotalItems.Should().Be(1);
        hydra.Type.Should().Be("Collection");

        var adjunct = hydra.Members![0];
        adjunct.Id.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId");
        adjunct.Asset.Should().Be($"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}");
        adjunct.IIIFLink.Should().Be("seeAlso");
        adjunct.Label.First().Key.Should().Be("label");
        adjunct.Language.Should().Contain(l => l == "en").And.HaveCount(1);
        adjunct.ExternalId.Should().Be("https://some-location.com/an-adjunct");
        adjunct.PublicId.Should().Be("https://some-location.com/an-adjunct");
        adjunct.Motivation.Should().Be("a motivation");
        adjunct.Provides.Should().Be("translation");

        response.Headers.Location.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts");

        var storage = await dbContext.CustomerStorages
            .SingleAsync(cs => cs.Customer == assetId.Customer && cs.Space == null);
        storage.NumberOfStoredAdjuncts.Should().Be(0, "external adjunct is not tracked in hosted storage count");
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
        error.Detail.Should().Be("Create failed. Adjunct or adjuncts with id(s) in (someAdjunctId) already exists");
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
        error.Detail.Should().Be("Create failed. Adjunct or adjuncts with id(s) in (someAdjunctId) already exists");
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
            .WithTestAdjunct("someAdjunctId", motivation: "a motivation");
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
        adjunct.Motivation.Should().Be("a motivation");
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

        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: assetId.Customer, numberOfAdjuncts: 0);
        await dbContext.SaveChangesAsync();

        const string newAdjunctJson = """
                                      {
                                                "@type": "Image",
                                                "origin": "https://some-location.com/an-adjunct",
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

        var storage = await dbContext.CustomerStorages
            .SingleAsync(cs => cs.Customer == assetId.Customer && cs.Space == null);
        storage.NumberOfStoredAdjuncts.Should().Be(0, "storage must not be modified when the asset does not exist");
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
        error.Detail.Should().Be($"Create failed. Adjunct or adjuncts with id(s) in ({adjunctId}) already exists");
    }
    
    [Fact]
    public async Task PutAdjunct_UpdatesAdjunct_WhenIdAlreadyExists()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct("someAdjunctId", created: DateTime.UtcNow.AddDays(-2), motivation: "something", provides: "something");
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
                                                   "motivation": "changed",
                                                   "provides": "changed"
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
        adjunct.Motivation.Should().Be("changed");
        adjunct.Provides.Should().Be("changed");
        
        A.CallTo(() => DeliverableNotificationSender.SendDeliverableModifiedMessage(
            A<IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>>>.That.Matches(c => c.Count == 1 && c.Single().ChangeType == ChangeType.Update && c.Single().After.AssetId == assetId), 
            A<CancellationToken>._)).MustHaveHappened();
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
        A.CallTo(() => DeliverableNotificationSender.SendDeliverableModifiedMessage(
            A<IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>>>.That.Matches(c => c.Count == 1  && c.Single().ChangeType == ChangeType.Create && c.Single().After.AssetId == assetId), 
            A<CancellationToken>._)).MustHaveHappened();
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
        await dbContext.CustomerStorages.AddTestCustomerStorage(numberOfAdjuncts: 2, sizeOfAdjuncts: 500);
        await dbContext.SaveChangesAsync();

        var path = $"{assetId.ToApiResourcePath()}/adjuncts/someAdjunctId";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).DeleteAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        A.CallTo(() => DeliverableNotificationSender.SendDeliverableModifiedMessage(
            A<NotificationRecord<DLCS.Model.Assets.Adjunct>>.That.Matches(r => r.ChangeType == ChangeType.Delete && r.Before.AssetId == assetId),
            A<CancellationToken>._)).MustHaveHappened();

        var storage = await dbContext.CustomerStorages
            .SingleAsync(cs => cs.Customer == assetId.Customer && cs.Space == null);
        storage.NumberOfStoredAdjuncts.Should().Be(2, "external adjunct deletion should not affect count");
        storage.TotalSizeOfStoredAdjuncts.Should().Be(500, "external adjunct deletion should not affect size");
    }
    
    [Fact]
    public async Task PostAdjunct_CreatesHostedAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.CustomerStorages.AddTestCustomerStorage();
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => IngestNotificationSender.SendIngestAdjunctRequest(
                A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>.That.Matches(a => a.Single().AssetId == assetId),
                A<CancellationToken>._))
            .ReturnsLazily(_ => Task.FromResult(1));
        
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
        var hydra = await response.ReadAsHydraResponseAsync<HydraCollection<Adjunct>>();

        hydra.Should().NotBeNull();
        hydra.Members.Should().HaveCount(1);

        var adjunct = hydra.Members![0];
        adjunct.Id.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/someAdjunctId");
        adjunct.IIIFLink.Should().Be("seeAlso");
        adjunct.Label.First().Key.Should().Be("label");
        adjunct.Language.Should().Contain(l => l == "en").And.HaveCount(1);
        adjunct.Origin.Should().Be("https://some-location.com/an-adjunct");
        adjunct.PublicId.Should().Be($"https://dlcs.digirati.io/adjuncts/99/1/{assetId.Asset}/someAdjunctId");
        adjunct.Ingesting.Should().Be(true, "the adjunct was sent to engine for ingestion");
        adjunct.Error.Should().BeNullOrEmpty("no errors yet");
        response.Headers.Location.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts");

        A.CallTo(() =>
                IngestNotificationSender.SendIngestAdjunctRequest(
                    A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>.That.Matches(a => a[0].AssetId == assetId && a[0].Id == "someAdjunctId"),
                    A<CancellationToken>._))
            .MustHaveHappened();

        var storage = await dbContext.CustomerStorages
            .SingleAsync(cs => cs.Customer == assetId.Customer && cs.Space == null);
        storage.NumberOfStoredAdjuncts.Should().Be(1, "hosted adjunct increments the count");
    }

    [Fact]
    public async Task PostAdjunct_CreatesMultipleHostedAdjuncts_AsArray()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();

        A.CallTo(() => IngestNotificationSender.SendIngestAdjunctRequest(
                A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>.That.Matches(a => a[0].AssetId == assetId),
                A<CancellationToken>._))
            .ReturnsLazily(_ => Task.FromResult(2));
        
        const string newAdjunctJson = """
                                      [
                                          {
                                            "id": "someAdjunctId1",
                                            "@type": "Image",
                                            "origin": "https://some-location.com/an-adjunct1",
                                            "iiifLink": "seeAlso",
                                            "mediaType": "a-mediaType",
                                            "label": {"label": ["value"]},
                                            "language": ["en"],
                                          },
                                          {
                                            "id": "someAdjunctId2",
                                            "@type": "Image",
                                            "origin": "https://some-location.com/an-adjunct2",
                                            "iiifLink": "seeAlso",
                                            "mediaType": "a-mediaType",
                                            "label": {"label": ["value"]},
                                            "language": ["en"],
                                          }
                                      ]
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.ReadAsHydraResponseAsync<HydraCollection<Adjunct>>();

        result.Should().NotBeNull("A collection result is expected");
        result.Members.Should().NotBeNullOrEmpty("two adjuncts are expected");

        for(var i = 0; i < result.Members.Length; i++)
        {
            var adjunct = result.Members[i];
            var adjunctId = $"someAdjunctId{i + 1}";
            adjunct.Id.Should()
                .Be(
                    $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/{adjunctId}");
            adjunct.IIIFLink.Should().Be("seeAlso");
            adjunct.Label.First().Key.Should().Be("label");
            adjunct.Language.Should().Contain(l => l == "en").And.HaveCount(1);
            adjunct.Origin.Should().Be($"https://some-location.com/an-adjunct{i+1}");
            adjunct.PublicId.Should().Be($"https://dlcs.digirati.io/adjuncts/99/1/{assetId.Asset}/{adjunctId}");
            adjunct.Ingesting.Should().Be(true, "the adjunct was sent to engine for ingestion");
            adjunct.Error.Should().BeNullOrEmpty("no errors yet");
        }

        A.CallTo(() =>
                IngestNotificationSender.SendIngestAdjunctRequest(
                    A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>.That.Matches(a => a.Count == 2 && a.All(ad => ad.AssetId == assetId)), 
                    A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        
        response.Headers.Location.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts");
    }
    
    [Fact]
    public async Task PostAdjunct_CreatesMultipleHostedAdjuncts_AsHydraCollection()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();

        A.CallTo(() => IngestNotificationSender.SendIngestAdjunctRequest(
                A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>.That.Matches(a => a[0].AssetId == assetId),
                A<CancellationToken>._))
            .ReturnsLazily(_ => Task.FromResult(2));

        const string newAdjunctJson = """
                                      {
                                          "@type": "Collection",
                                          "member":
                                              [
                                                  {
                                                    "id": "someAdjunctId1",
                                                    "@type": "Image",
                                                    "origin": "https://some-location.com/an-adjunct1",
                                                    "iiifLink": "seeAlso",
                                                    "mediaType": "a-mediaType",
                                                    "label": {"label": ["value"]},
                                                    "language": ["en"],
                                                  },
                                                  {
                                                    "id": "someAdjunctId2",
                                                    "@type": "Image",
                                                    "origin": "https://some-location.com/an-adjunct2",
                                                    "iiifLink": "seeAlso",
                                                    "mediaType": "a-mediaType",
                                                    "label": {"label": ["value"]},
                                                    "language": ["en"],
                                                  }
                                              ]
                                      }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.ReadAsHydraResponseAsync<HydraCollection<Adjunct>>();

        result.Should().NotBeNull("A collection result is expected");
        result.Members.Should().NotBeNullOrEmpty("two adjuncts are expected");

        for(var i = 0; i < result.Members.Length; i++)
        {
            var adjunct = result.Members[i];
            var adjunctId = $"someAdjunctId{i + 1}";

            adjunct.Id.Should()
                .Be(
                    $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts/{adjunctId}");
            adjunct.IIIFLink.Should().Be("seeAlso");
            adjunct.Label.First().Key.Should().Be("label");
            adjunct.Language.Should().Contain(l => l == "en").And.HaveCount(1);
            adjunct.Origin.Should().Be($"https://some-location.com/an-adjunct{i+1}");
            adjunct.PublicId.Should().Be($"https://dlcs.digirati.io/adjuncts/99/1/{assetId.Asset}/{adjunctId}");
            adjunct.Ingesting.Should().Be(true, "the adjunct was sent to engine for ingestion");
            adjunct.Error.Should().BeNullOrEmpty("no errors yet");
        }
        
        A.CallTo(() =>
                IngestNotificationSender.SendIngestAdjunctRequest(
                    A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>.That.Matches(a => a.Count == 2),
                    A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        
        response.Headers.Location.Should()
            .Be(
                $"http://localhost/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/adjuncts");
    }
    
    [Fact]
    public async Task PostAdjunct_FailsToCreateMultipleHostedAdjuncts_WhenEmptyArray()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = """
                                      [
                                          
                                      ]
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Func<Task> tryRead = () => response.ReadAsHydraResponseAsync<Error>();
        (await tryRead.Should().ThrowAsync<DlcsException>()).WithMessage(
            "One or more adjuncts were expected in request body but found none");
    }
    
    [Fact]
    public async Task PostAdjunct_FailsToCreateMultipleHostedAdjuncts_WhenOneFailsValidation()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        // first is missing iiifLink:
        const string newAdjunctJson = """
                                      [
                                          {
                                            "id": "someAdjunctId1",
                                            "@type": "Image",
                                            "origin": "https://some-location.com/an-adjunct1",
                                            "mediaType": "a-mediaType",
                                            "label": {"label": ["value"]},
                                            "language": ["en"],
                                          },
                                          {
                                            "id": "someAdjunctId2",
                                            "@type": "Image",
                                            "origin": "https://some-location.com/an-adjunct2",
                                            "iiifLink": "seeAlso",
                                            "mediaType": "a-mediaType",
                                            "label": {"label": ["value"]},
                                            "language": ["en"],
                                          }
                                      ]
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Func<Task> tryRead = () => response.ReadAsHydraResponseAsync<Error>();
        (await tryRead.Should().ThrowAsync<DlcsException>()).WithMessage("'iiifLink' is required");
        
        // verify db unchanged
        dbContext.Adjuncts.Count(a=>a.AssetId == assetId).Should().Be(0, "no adjuncts for this asset should have been created");
    }
    
    [Fact]
    public async Task PostAdjunct_FailsToCreateMultipleHostedAdjuncts_WhenOneExists()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct("someAdjunctId2", origin:"https://some-location.com/an-adjunct1"); 
        await dbContext.SaveChangesAsync();
        
        // second already exists
        const string newAdjunctJson = """
                                      [
                                          {
                                            "id": "someAdjunctId1",
                                            "@type": "Image",
                                            "origin": "https://some-location.com/an-adjunct1",
                                            "iiifLink": "seeAlso",
                                            "mediaType": "a-mediaType",
                                            "label": {"label": ["value"]},
                                            "language": ["en"],
                                          },
                                          {
                                            "id": "someAdjunctId2",
                                            "@type": "Image",
                                            "origin": "https://some-location.com/an-adjunct2",
                                            "iiifLink": "seeAlso",
                                            "mediaType": "a-mediaType",
                                            "label": {"label": ["value"]},
                                            "language": ["en"],
                                          }
                                      ]
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        // NOTE: Post is "create only"!
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Func<Task> tryRead = () => response.ReadAsHydraResponseAsync<Error>();
        (await tryRead.Should().ThrowAsync<DlcsException>())
            .WithMessage("Create failed. Adjunct or adjuncts with id(s) in (someAdjunctId1,someAdjunctId2) already exists");
        
        // verify db unchanged
        dbContext.Adjuncts.Count(a=>a.AssetId == assetId).Should().Be(1, "no additional adjuncts for this asset should have been created");
    }
    
    [Fact]
    public async Task PostAdjunct_FailsToCreateMultipleHostedAdjuncts_WhenAssetNotExist()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        
        // second already exists
        const string newAdjunctJson = """
                                      [
                                          {
                                            "id": "someAdjunctId1",
                                            "@type": "Image",
                                            "origin": "https://some-location.com/an-adjunct1",
                                            "iiifLink": "seeAlso",
                                            "mediaType": "a-mediaType",
                                            "label": {"label": ["value"]},
                                            "language": ["en"],
                                          },
                                          {
                                            "id": "someAdjunctId2",
                                            "@type": "Image",
                                            "origin": "https://some-location.com/an-adjunct2",
                                            "iiifLink": "seeAlso",
                                            "mediaType": "a-mediaType",
                                            "label": {"label": ["value"]},
                                            "language": ["en"],
                                          }
                                      ]
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        // NOTE: Post is "create only"!
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Func<Task> tryRead = () => response.ReadAsHydraResponseAsync<Error>();
        (await tryRead.Should().ThrowAsync<DlcsException>())
            .WithMessage($"Asset with id '{assetId}' not found");
    }
    
    [Fact]
    public async Task PostAdjunct_FailsToCreateMultipleHostedAdjuncts_WhenEmptyCollection()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();
        
        const string newAdjunctJson = """
                                      {
                                          "@type": "Collection",
                                          "member": []
                                      }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Func<Task> tryRead = () => response.ReadAsHydraResponseAsync<Error>();
        (await tryRead.Should().ThrowAsync<DlcsException>())
            .WithMessage("One or more adjuncts were expected in request body but found none");
    }
    
    [Fact]
    public async Task PostAdjunct_FailsToCreateMultipleHostedAdjuncts_WhenIngestFail()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();
        
        // Only 1 succeeded
        A.CallTo(() => IngestNotificationSender.SendIngestAdjunctRequest(
                A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>.That.Matches(a => a[0].AssetId == assetId),
                A<CancellationToken>._))
            .ReturnsLazily(_ => Task.FromResult(1));
        
        const string newAdjunctJson = """
                                      [
                                          {
                                            "id": "someAdjunctId1",
                                            "@type": "Image",
                                            "origin": "https://some-location.com/an-adjunct1",
                                            "iiifLink": "seeAlso",
                                            "mediaType": "a-mediaType",
                                            "label": {"label": ["value"]},
                                            "language": ["en"],
                                          },
                                          {
                                            "id": "someAdjunctId2",
                                            "@type": "Image",
                                            "origin": "https://some-location.com/an-adjunct2",
                                            "iiifLink": "seeAlso",
                                            "mediaType": "a-mediaType",
                                            "label": {"label": ["value"]},
                                            "language": ["en"],
                                          }
                                      ]
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act
        // NOTE: Post is "create only"!
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        Func<Task> tryRead = () => response.ReadAsHydraResponseAsync<Error>();
        (await tryRead.Should().ThrowAsync<DlcsException>())
            .WithMessage($"One or more adjuncts for asset {assetId} failed submission for ingestion and will need to be resubmitted");
        
        // verify db status
        dbContext.Adjuncts.Count(a=>a.AssetId == assetId).Should().Be(2, "creation in db worked, but we failed after saving");
    }
    
    [Fact]
    public async Task PostAdjunct_UpdateHostedToHosted()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.Images.AddTestAsset(assetId); 
        await dbContext.SaveChangesAsync();

        A.CallTo(() => IngestNotificationSender.SendIngestAdjunctRequest(
                A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>.That.Matches(a => a.Single().AssetId == assetId),
                A<CancellationToken>._))
            .ReturnsLazily(_ => Task.FromResult(1));
        
        const string adjunctId = "updateableAdjunct";
        const string adjunctOrigin = "https://example.com/an-adjunct";
        
        // we provide "size", but as this will be ingested the API should remove it, checked below
        const string newAdjunctJson = $$"""
                                      {
                                                "id": "{{adjunctId}}",
                                                "@type": "Image",
                                                "origin": "{{adjunctOrigin}}",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                                "size": 67
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act 1
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert 1
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var hydra = await response.ReadAsHydraResponseAsync<HydraCollection<Adjunct>>();

        hydra.Should().NotBeNull();
        hydra.Members.Should().HaveCount(1);

        var adjunct = hydra.Members![0];
        adjunct.Size.Should().BeNull("hosted adjunct's size will be determined and updated by the engine");
        adjunct.Ingesting.Should().Be(true, "the adjunct was sent to engine for ingestion");
        adjunct.Error.Should().BeNullOrEmpty("no errors yet");
        adjunct.Finished.Should().BeNull("will be set by the engine when done, and it's a new adjunct");
        
        // update the adjunct to simulate Engine
        var dbAdjunct = dbContext.Adjuncts.Single(a => a.Id == adjunctId && a.AssetId == assetId);
        dbAdjunct.Size = 1234L; // some value determined by the engine
        dbAdjunct.Ingesting = false;
        dbAdjunct.Finished = DateTime.UtcNow;
        // no error, "ingest" was successful
        dbContext.Entry(dbAdjunct).State = EntityState.Modified;
        await dbContext.SaveChangesAsync();
        
        // update json, e.g. we change the origin
        const string updatedAdjunctJson = $$"""
                                        {
                                                  "id": "{{adjunctId}}",
                                                  "@type": "Image",
                                                  "origin": "{{adjunctOrigin + "2"}}",
                                                  "iiifLink": "seeAlso",
                                                  "mediaType": "a-mediaType",
                                                  "label": {"label": ["value"]},
                                                  "language": ["en"],
                                                  "size": 69
                                                }
                                        """;
        content = new StringContent(updatedAdjunctJson, Encoding.UTF8, "application/json");
        
        // Act 2
        // we use put to update, passing the adjunctId in the path
        response = await httpClient.AsCustomer(assetId.Customer).PutAsync($"{path}/{adjunctId}", content);

        // Assert 2
        response.StatusCode.Should().Be(HttpStatusCode.OK); // not created, updated
        adjunct = await response.ReadAsHydraResponseAsync<Adjunct>();

        adjunct.Size.Should().Be(1234L, "API preserves the size set by engine");
        adjunct.Ingesting.Should().Be(true, "the adjunct was sent to engine for (re)ingestion");
        adjunct.Finished.Should().NotBeNull("API doesn't touch finished, as it now states for 'last finished'");
    }
    
    [Fact]
    public async Task PostAdjunct_UpdateExternalToHosted()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.CustomerStorages.AddTestCustomerStorage();
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => IngestNotificationSender.SendIngestAdjunctRequest(
                A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>.That.Matches(a => a.Single().AssetId == assetId),
                A<CancellationToken>._))
            .ReturnsLazily(_ => Task.FromResult(1));
        
        const string adjunctId = "exToHostAdjunct";
        const string adjunctOrigin = "https://example.com/an-adjunct";
        const string externalUri = "https://example.com/some-external-id";
        
        // we provide "size", but as this will be ingested the API should remove it, checked below
        const string newAdjunctJson = $$"""
                                      {
                                                "id": "{{adjunctId}}",
                                                "@type": "Image",
                                                "externalId": "{{externalUri}}",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                                "size": 67
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act 1
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert 1
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var hydra = await response.ReadAsHydraResponseAsync<HydraCollection<Adjunct>>();

        hydra.Should().NotBeNull();
        hydra.Members.Should().HaveCount(1);

        var adjunct = hydra.Members![0];
        
        adjunct.Size.Should().Be(67, "we 'trust' external adjunct's submitted size declaration");
        adjunct.Ingesting.Should().NotBe(true, "the adjunct was NOT sent to engine for ingestion");
        adjunct.Error.Should().BeNullOrEmpty("not ingested");
        adjunct.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        adjunct.ExternalId.Should().Be(externalUri);
        adjunct.PublicId.Should().Be(externalUri);
        
        // update json, now it's gonna be a hosted - we removed externalId and added origin
        const string updatedAdjunctJson = $$"""
                                        {
                                                  "id": "{{adjunctId}}",
                                                  "@type": "Image",
                                                  "origin": "{{adjunctOrigin}}",
                                                  "iiifLink": "seeAlso",
                                                  "mediaType": "a-mediaType",
                                                  "label": {"label": ["value"]},
                                                  "language": ["en"],
                                                  "size": 69
                                                }
                                        """;
        content = new StringContent(updatedAdjunctJson, Encoding.UTF8, "application/json");
        
        // Act 2
        // we use put to update, passing the adjunctId in the path
        response = await httpClient.AsCustomer(assetId.Customer).PutAsync($"{path}/{adjunctId}", content);

        // Assert 2
        response.StatusCode.Should().Be(HttpStatusCode.OK); // not created, updated
        adjunct = await response.ReadAsHydraResponseAsync<Adjunct>();

        adjunct.Size.Should().Be(null, "API sets to null to signify it wasn't hosted previously - so no size for our purposes");
        adjunct.Ingesting.Should().Be(true, "the adjunct was sent to engine for ingestion");
        adjunct.Finished.Should().NotBeNull("API doesn't touch finished, as it now states for 'last finished'");
        adjunct.ExternalId.Should().BeNull("we removed it in the update json");
        adjunct.PublicId.Should().NotBe(externalUri, "it no longer points to the external id from initial creation");

        var storage = await dbContext.CustomerStorages
            .SingleAsync(cs => cs.Customer == assetId.Customer && cs.Space == null);
        storage.NumberOfStoredAdjuncts.Should().Be(1, "transitioning from external to hosted increments count");
    }

    [Fact]
    public async Task PostAdjunct_UpdateHostedToExternal()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        await dbContext.CustomerStorages.AddTestCustomerStorage();
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => IngestNotificationSender.SendIngestAdjunctRequest(
                A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>.That.Matches(a => a.Single().AssetId == assetId),
                A<CancellationToken>._))
            .ReturnsLazily(_ => Task.FromResult(1));

        const string adjunctId = "updateableAdjunct";
        const string adjunctOrigin = "https://example.com/an-adjunct";
        const string externalUri = "https://example.com/some-external-id";

        
        // we provide "size", but as this will be ingested the API should remove it, checked below
        const string newAdjunctJson = $$"""
                                      {
                                                "id": "{{adjunctId}}",
                                                "@type": "Image",
                                                "origin": "{{adjunctOrigin}}",
                                                "iiifLink": "seeAlso",
                                                "mediaType": "a-mediaType",
                                                "label": {"label": ["value"]},
                                                "language": ["en"],
                                                "size": 67
                                              }
                                      """;
        
        var path = $"{assetId.ToApiResourcePath()}/adjuncts";
        var content = new StringContent(newAdjunctJson, Encoding.UTF8, "application/json");

        // Act 1
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync(path, content);

        // Assert 1
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var hydra = await response.ReadAsHydraResponseAsync<HydraCollection<Adjunct>>();

        hydra.Should().NotBeNull();
        hydra.Members.Should().HaveCount(1);

        var adjunct = hydra.Members![0];
        adjunct.Size.Should().BeNull("hosted adjunct's size will be determined and updated by the engine");
        adjunct.Ingesting.Should().Be(true, "the adjunct was sent to engine for ingestion");
        adjunct.Error.Should().BeNullOrEmpty("no errors yet");
        adjunct.Finished.Should().BeNull("will be set by the engine when done, and it's a new adjunct");
        
        // update the adjunct to simulate Engine
        var dbAdjunct = dbContext.Adjuncts.Single(a => a.Id == adjunctId && a.AssetId == assetId);
        dbAdjunct.Size = 1234L; // some value determined by the engine
        dbAdjunct.Ingesting = false;
        dbAdjunct.Finished = DateTime.UtcNow;
        // no error, "ingest" was successful
        dbContext.Entry(dbAdjunct).State = EntityState.Modified;
        await dbContext.SaveChangesAsync();

        // update json, this time it will be an external one
        const string updatedAdjunctJson = $$"""
                                        {
                                                  "id": "{{adjunctId}}",
                                                  "@type": "Image",
                                                  "externalId": "{{externalUri}}",
                                                  "iiifLink": "seeAlso",
                                                  "mediaType": "a-mediaType",
                                                  "label": {"label": ["value"]},
                                                  "language": ["en"],
                                                  "size": 69
                                                }
                                        """;
        content = new StringContent(updatedAdjunctJson, Encoding.UTF8, "application/json");
        
        // Act 2
        // we use put to update, passing the adjunctId in the path
        response = await httpClient.AsCustomer(assetId.Customer).PutAsync($"{path}/{adjunctId}", content);

        // Assert 2
        response.StatusCode.Should().Be(HttpStatusCode.OK); // not created, updated
        adjunct = await response.ReadAsHydraResponseAsync<Adjunct>();

        adjunct.Size.Should().Be(69L, "as this is now external adjunct, ");
        adjunct.Ingesting.Should().NotBe(true, "the adjunct was NOT sent to engine for ingestion");
        adjunct.Finished.Should().NotBeNull("API doesn't touch finished, as it now states for 'last finished'");

        var storage = await dbContext.CustomerStorages
            .SingleAsync(cs => cs.Customer == assetId.Customer && cs.Space == null);
        storage.NumberOfStoredAdjuncts.Should().Be(0, "transitioning from hosted to external decrements count");
    }

    [Fact]
    public async Task PutAdjunct_DecrementsStorageSize_WhenHostedAdjunctBecomesExternal()
    {
        // Arrange — seed adjunct with origin+size directly so storage arithmetic is deterministic without a preceding API call
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "hosted-to-ext-size";
        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId, origin: "https://example.com/file.jpg", size: 2048);
        await dbContext.CustomerStorages.AddTestCustomerStorage(numberOfAdjuncts: 1, sizeOfAdjuncts: 2048);
        await dbContext.CustomerStorages.AddTestCustomerStorage(space: assetId.Space, numberOfAdjuncts: 1, sizeOfAdjuncts: 2048);
        await dbContext.SaveChangesAsync();

        var json = $$"""
                     {
                       "id": "{{adjunctId}}",
                       "@type": "Image",
                       "externalId": "https://example.com/external",
                       "iiifLink": "seeAlso",
                       "mediaType": "image/jpeg"
                     }
                     """;

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer)
            .PutAsync($"{assetId.ToApiResourcePath()}/adjuncts/{adjunctId}",
                new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var storage = await dbContext.CustomerStorages
            .SingleAsync(cs => cs.Customer == assetId.Customer && cs.Space == null);
        storage.NumberOfStoredAdjuncts.Should().Be(0, "aggregate count decremented on hosted→external transition");
        storage.TotalSizeOfStoredAdjuncts.Should().Be(0, "aggregate size decremented by adjunct's stored size on hosted→external transition");

        var spaceStorage = await dbContext.CustomerStorages
            .SingleAsync(cs => cs.Customer == assetId.Customer && cs.Space == assetId.Space);
        spaceStorage.NumberOfStoredAdjuncts.Should().Be(0, "per-space count decremented on hosted→external transition");
        spaceStorage.TotalSizeOfStoredAdjuncts.Should().Be(0, "per-space size decremented by adjunct's stored size on hosted→external transition");
    }

    [Fact]
    public async Task DeleteAdjunct_ReducesCustomerStorage_WhenHostedAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "hosted-del";
        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId, origin: "https://example.com/file.jpg", size: 1024);
        await dbContext.CustomerStorages.AddTestCustomerStorage(numberOfAdjuncts: 1, sizeOfAdjuncts: 1024);
        await dbContext.CustomerStorages.AddTestCustomerStorage(space: assetId.Space, numberOfAdjuncts: 1, sizeOfAdjuncts: 1024);
        await dbContext.SaveChangesAsync();

        // Act
        await httpClient.AsCustomer(assetId.Customer)
            .DeleteAsync($"{assetId.ToApiResourcePath()}/adjuncts/{adjunctId}");

        // Assert
        var storage = await dbContext.CustomerStorages
            .SingleAsync(cs => cs.Customer == assetId.Customer && cs.Space == null);
        storage.NumberOfStoredAdjuncts.Should().Be(0, "aggregate count decremented on delete");
        storage.TotalSizeOfStoredAdjuncts.Should().Be(0, "aggregate size decremented on delete");

        var spaceStorage = await dbContext.CustomerStorages
            .SingleAsync(cs => cs.Customer == assetId.Customer && cs.Space == assetId.Space);
        spaceStorage.NumberOfStoredAdjuncts.Should().Be(0, "per-space count decremented on delete");
        spaceStorage.TotalSizeOfStoredAdjuncts.Should().Be(0, "per-space size decremented on delete");
    }

}
