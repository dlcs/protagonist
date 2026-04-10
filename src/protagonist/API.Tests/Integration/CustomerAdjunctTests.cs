using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using API.Infrastructure;
using API.Infrastructure.Messaging.General;
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
public class CustomerAdjunctTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dbContext;

    private static readonly IDeliverableNotificationSender DeliverableNotificationSender =
        A.Fake<IDeliverableNotificationSender>();

    public CustomerAdjunctTests(StorageFixture storageFixture, ProtagonistAppFactory<Startup> factory)
    {
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient
        (
            storageFixture.DbFixture, "API-Test",
            f => f.WithLocalStack(storageFixture.LocalStackFixture
            ).WithTestServices(services =>
            {
                services.AddAuthentication("API-Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "API-Test", o => { o.TimeProvider = TimeProvider.System; });
                services.AddSingleton(_ => DeliverableNotificationSender);
            }));
        dbContext = storageFixture.DbFixture.DbContext;
        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task DeleteMultipleAdjuncts_Returns200_WhenDeletingSingleAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "someAdjunctId";

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId);
        await dbContext.SaveChangesAsync();

        var deleteAdjunctsJson = $$"""
                                   {
                                       "@type": "Collection",
                                       "member": [ 
                                         { "id": "{{assetId}}", "adjunct": [ "{{adjunctId}}" ] }
                                       ]
                                   };
                                   """;

        var content = new StringContent(deleteAdjunctsJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync($"/customers/{assetId.Customer}/deleteAdjuncts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        A.CallTo(() => DeliverableNotificationSender.SendDeliverableModifiedMessage(
            A<IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>>>.That.Matches(r =>
                r.First().ChangeType == ChangeType.Delete && r.First().Before.Id == adjunctId),
            A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task DeleteMultipleAdjuncts_Returns200_WhenDeletingMultipleAdjuncts()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "someAdjunctId";

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId)
            .WithTestAdjunct($"{adjunctId}_1");
        await dbContext.SaveChangesAsync();

        var deleteAdjunctsJson = $$"""
                                   {
                                       "@type": "Collection",
                                       "member": [ 
                                         { "id": "{{assetId}}", "adjunct": [ "{{adjunctId}}", "{{adjunctId}}_1" ] },
                                       ]
                                   };
                                   """;

        var content = new StringContent(deleteAdjunctsJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync($"/customers/{assetId.Customer}/deleteAdjuncts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        A.CallTo(() => DeliverableNotificationSender.SendDeliverableModifiedMessage(
            A<IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>>>.That.Matches(r =>
                r.First().ChangeType == ChangeType.Delete && r.First().Before.Id == adjunctId && r.Last().Before.Id == $"{adjunctId}_1"),
            A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task DeleteMultipleAdjuncts_Returns200_WhenDeletingMultipleAdjunctsAcrossAssets()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "someAdjunctId";

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId)
            .WithTestAdjunct($"{adjunctId}_1");
        await dbContext.SaveChangesAsync();

        var deleteAdjunctsJson = $$"""
                                   {
                                       "@type": "Collection",
                                       "member": [ 
                                         { "id": "{{assetId}}", "adjunct": [ "{{adjunctId}}" ] },
                                         { "id": "{{assetId}}", "adjunct": [ "{{adjunctId}}_1" ] }
                                       ]
                                   };
                                   """;

        var content = new StringContent(deleteAdjunctsJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync($"/customers/{assetId.Customer}/deleteAdjuncts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        A.CallTo(() => DeliverableNotificationSender.SendDeliverableModifiedMessage(
            A<IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>>>.That.Matches(r =>
                r.First().ChangeType == ChangeType.Delete && r.First().Before.Id == adjunctId && r.Last().Before.Id == $"{adjunctId}_1"),
            A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task DeleteMultipleAdjuncts_Returns200_WhenDeletingMultipleAdjunctsAcrossMultipleAssets()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var secondAssetId = AssetIdGenerator.GetAssetId(asset: $"{nameof(DeleteMultipleAdjuncts_Returns200_WhenDeletingMultipleAdjunctsAcrossMultipleAssets)}_1");
        const string adjunctId = "someAdjunctId";

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId)
            .WithTestAdjunct($"{adjunctId}_1");
        await dbContext.Images.AddTestAsset(secondAssetId)
            .WithTestAdjunct(adjunctId)
            .WithTestAdjunct($"{adjunctId}_1");
        await dbContext.SaveChangesAsync();

        var deleteAdjunctsJson = $$"""
                                   {
                                       "@type": "Collection",
                                       "member": [ 
                                         { "id": "{{assetId}}", "adjunct": [ "{{adjunctId}}", "{{adjunctId}}_1" ] },
                                         { "id": "{{secondAssetId}}", "adjunct": [ "{{adjunctId}}", "{{adjunctId}}_1" ] }
                                       ]
                                   };
                                   """;

        var content = new StringContent(deleteAdjunctsJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync($"/customers/{assetId.Customer}/deleteAdjuncts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        A.CallTo(() => DeliverableNotificationSender.SendDeliverableModifiedMessage(
            A<IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>>>.That.Matches(r =>
                r.First().ChangeType == ChangeType.Delete && r.First().Before.Id == adjunctId && r.First().Before.AssetId == assetId && 
                r.Last().Before.Id == $"{adjunctId}_1" && r.Last().Before.AssetId == secondAssetId && r.Count == 4),
            A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task DeleteMultipleAdjuncts_Returns400_WhenDeletingNonExistentAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "someAdjunctId";

        var deleteAdjunctsJson = $$"""
                                   {
                                       "@type": "Collection",
                                       "member": [ 
                                         { "id": "{{assetId}}", "adjunct": [ "{{adjunctId}}" ] }
                                       ]
                                   };
                                   """;

        var content = new StringContent(deleteAdjunctsJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync($"/customers/{assetId.Customer}/deleteAdjuncts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        A.CallTo(() => DeliverableNotificationSender.SendDeliverableModifiedMessage(
            A<IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>>>.That.Matches(r =>
                r.First().ChangeType == ChangeType.Delete && r.First().Before.Id == adjunctId && r.First().Before.AssetId == assetId),
            A<CancellationToken>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task DeleteMultipleAdjuncts_Returns200_WhenDeletingMixOfExistingAndNonExistingAdjuncts()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "someAdjunctId";

        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId);
        await dbContext.SaveChangesAsync();

        var deleteAdjunctsJson = $$"""
                                   {
                                       "@type": "Collection",
                                       "member": [ 
                                         { "id": "{{assetId}}", "adjunct": [ "{{adjunctId}}", "{{adjunctId}}_1" ] }
                                       ]
                                   };
                                   """;

        var content = new StringContent(deleteAdjunctsJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer).PostAsync($"/customers/{assetId.Customer}/deleteAdjuncts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        A.CallTo(() => DeliverableNotificationSender.SendDeliverableModifiedMessage(
            A<IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>>>.That.Matches(r =>
                r.First().ChangeType == ChangeType.Delete && r.First().Before.Id == adjunctId && r.Count == 1),
            A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task DeleteMultipleAdjuncts_Returns400_WhenDeletingAdjunctWithDifferentCustomerId()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(customer: 1);
        const string adjunctId = "someAdjunctId";

        var deleteAdjunctsJson = $$"""
                                   {
                                       "@type": "Collection",
                                       "member": [ 
                                         { "id": "{{assetId}}", "adjunct": [ "{{adjunctId}}" ] }
                                       ]
                                   };
                                   """;

        var content = new StringContent(deleteAdjunctsJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(99).PostAsync($"/customers/99/deleteAdjuncts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Description.Should().Be($"Asset id '{assetId}' cannot belong to a different customer");
        
        A.CallTo(() => DeliverableNotificationSender.SendDeliverableModifiedMessage(
            A<IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>>>.That.Matches(r =>
                r.First().ChangeType == ChangeType.Delete && r.First().Before.Id == adjunctId && r.First().Before.AssetId == assetId),
            A<CancellationToken>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task DeleteMultipleAdjuncts_Returns400_WhenAssetIdNotAnAssetId()
    {
        // Arrange
        const string adjunctId = nameof(DeleteMultipleAdjuncts_Returns400_WhenAssetIdNotAnAssetId);

        var deleteAdjunctsJson = $$"""
                                   {
                                       "@type": "Collection",
                                       "member": [ 
                                         { "id": "this/is/not/a/valid/asset/id", "adjunct": [ "{{adjunctId}}" ] }
                                       ]
                                   };
                                   """;

        var content = new StringContent(deleteAdjunctsJson, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.AsCustomer(99).PostAsync($"/customers/99/deleteAdjuncts", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.ReadAsJsonAsync<Error>(ensureSuccess: false);
        error.Description.Should().Be("AssetId 'this/is/not/a/valid/asset/id' is invalid. Must be in format customer/space/asset");
        
        A.CallTo(() => DeliverableNotificationSender.SendDeliverableModifiedMessage(
            A<IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>>>.That.Matches(r =>
                r.First().ChangeType == ChangeType.Delete && r.First().Before.Id == adjunctId),
            A<CancellationToken>._)).MustNotHaveHappened();
    }
}
