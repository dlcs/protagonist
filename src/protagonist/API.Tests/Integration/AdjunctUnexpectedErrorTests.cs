using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Types;
using DLCS.Model.Storage;
using DLCS.Repository;
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
public class AdjunctUnexpectedErrorTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dbContext;

    private static readonly IStorageRepository StorageRepository = A.Fake<IStorageRepository>();

    public AdjunctUnexpectedErrorTests(StorageFixture storageFixture, ProtagonistAppFactory<Startup> factory)
    {
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(
            storageFixture.DbFixture, "API-Test",
            f => f.WithLocalStack(storageFixture.LocalStackFixture)
                .WithTestServices(services =>
                {
                    services.AddAuthentication("API-Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                            "API-Test", o => { o.TimeProvider = TimeProvider.System; });
                    services.AddSingleton<IStorageRepository>(_ => StorageRepository);
                }));
        dbContext = storageFixture.DbFixture.DbContext;
        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task PutAdjunct_Returns500_WithCorrectMessage_WhenUnexpectedErrorOccurs()
    {
        // Arrange — seed a hosted adjunct (has an origin) so that a PUT without origin triggers hosted→external transition,
        // causing DecrementAdjunctStorage to be called. The fake throws to simulate an unexpected error.
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "hosted-adjunct";

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId, origin: "https://example.com/file.jpg", size: 1024);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => StorageRepository.DecrementAdjunctStorage(
                A<AssetId>._, A<long>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Simulated unexpected error"));

        // PUT the adjunct without origin to trigger the hosted→external transition
        const string updateJson = $$"""
                                    {
                                      "id": "{{adjunctId}}",
                                      "@type": "Image",
                                      "externalId": "https://example.com/external",
                                      "iiifLink": "seeAlso",
                                      "mediaType": "image/jpeg"
                                    }
                                    """;

        var path = $"{assetId.ToApiResourcePath()}/adjuncts/{adjunctId}";
        var content = new StringContent(updateJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        Func<Task> tryRead = () => response.ReadAsHydraResponseAsync<Error>();
        (await tryRead.Should().ThrowAsync<DlcsException>()).WithMessage(
            $"Unknown error processing adjuncts for {assetId}");
    }
}
