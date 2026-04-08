using System.Net;
using System.Net.Http;
using System.Text;
using API.Infrastructure;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Messaging;
using DLCS.Repository;
using DLCS.Web.Response;
using FakeItEasy;
using Hydra.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class CustomerAdjunctQueueTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    // TODO - remove / tweak as required. This was all copied from CustomerQueueTests
    private readonly HttpClient httpClient;
    private readonly DlcsContext dbContext;
    private static readonly IDeliverableNotificationSender DeliverableNotificationSender = A.Fake<IDeliverableNotificationSender>();
    private static readonly IIngestNotificationSender IngestNotificationSender = A.Fake<IIngestNotificationSender>();

    public CustomerAdjunctQueueTests(StorageFixture storageFixture, ProtagonistAppFactory<Startup> factory)
    {
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(
            storageFixture.DbFixture, "API-Test",
            f => f.WithLocalStack(storageFixture.LocalStackFixture)
                .WithTestServices(services =>
                {
                    services.AddAuthentication("API-Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                            "API-Test", o => { });
                    services.AddSingleton(_ => DeliverableNotificationSender);
                    services.AddScoped(_ => IngestNotificationSender);
                }));
        dbContext = storageFixture.DbFixture.DbContext;
        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task PostAdjunctBatch_Returns400_WhenMembersEmpty()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const string json = """{ "member": [] }""";

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer)
            .PostAsync($"/customers/{assetId.Customer}/adjunctQueue",
                new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAdjunctBatch_Returns400_WhenAssetFieldMissing()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const string json = """
                            {
                              "member": [{
                                "id": "adj-1",
                                "@type": "Image",
                                "mediaType": "image/jpeg",
                                "iiifLink": "seeAlso",
                                "externalId": "https://example.com/adj"
                              }]
                            }
                            """;

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer)
            .PostAsync($"/customers/{assetId.Customer}/adjunctQueue",
                new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Detail.Should().Contain("asset");
    }

    [Theory]
    [InlineData("not-valid")]
    [InlineData("just/two")]
    [InlineData("https://example.com/no/asset/path")]
    public async Task PostAdjunctBatch_Returns400_WhenAssetIdInvalidFormat(string invalidAsset)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var json = $$"""
                     {
                       "member": [{
                         "id": "adj-1",
                         "asset": "{{invalidAsset}}",
                         "@type": "Image",
                         "mediaType": "image/jpeg",
                         "iiifLink": "seeAlso",
                         "externalId": "https://example.com/adj"
                       }]
                     }
                     """;

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer)
            .PostAsync($"/customers/{assetId.Customer}/adjunctQueue",
                new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAdjunctBatch_Returns400_WhenAssetBelongsToDifferentCustomer()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const int differentCustomer = 1;
        var json = $$"""
                     {
                       "member": [{
                         "id": "adj-1",
                         "asset": "{{differentCustomer}}/1/some-asset",
                         "@type": "Image",
                         "mediaType": "image/jpeg",
                         "iiifLink": "seeAlso",
                         "externalId": "https://example.com/adj"
                       }]
                     }
                     """;

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer)
            .PostAsync($"/customers/{assetId.Customer}/adjunctQueue",
                new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Detail.Should().Contain($"does not belong to customer {assetId.Customer}");
    }
}
