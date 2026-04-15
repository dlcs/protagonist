using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using API.Client;
using API.Infrastructure;
using API.Tests.Integration.Infrastructure;
using DLCS.AWS.SNS.Messaging;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository;
using DLCS.Web.Response;
using FakeItEasy;
using Hydra.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;
using AdjunctBatch = DLCS.HydraModel.AdjunctBatch;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class CustomerAdjunctQueueTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dbContext;
    private static readonly IDeliverableNotificationSender DeliverableNotificationSender = A.Fake<IDeliverableNotificationSender>();
    private static readonly IIngestNotificationSender IngestNotificationSender = A.Fake<IIngestNotificationSender>();
    private static readonly IBatchCompletedNotificationSender BatchCompletedNotificationSender = A.Fake<IBatchCompletedNotificationSender>();

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
                    services.AddScoped(_ => BatchCompletedNotificationSender);
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

    [Fact]
    public async Task PostAdjunctBatch_Returns404_WhenAssetDoesNotExist()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var json = $$"""
                     {
                       "member": [{
                         "id": "adj-1",
                         "asset": "{{assetId}}",
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
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostAdjunctBatch_Returns201_WithBatch_ForSingleExternalAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();

        var json = $$"""
                     {
                       "member": [{
                         "id": "adj-1",
                         "asset": "{{assetId}}",
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
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var batch = await response.ReadAsHydraResponseAsync<AdjunctBatch>();
        batch.Count.Should().Be(1);
        batch.Completed.Should().Be(1, "external adjuncts complete immediately");
        batch.Errors.Should().Be(0);
        batch.Finished.Should().NotBeNull("all adjuncts are external so batch is finished");

        var batchId = ParseBatchId(batch);
        var dbBatch = await dbContext.AdjunctBatches.AsNoTracking().SingleAsync(b => b.Id == batchId);
        dbBatch.Count.Should().Be(1);
        dbBatch.Completed.Should().Be(1);

        var junctionRecord = await dbContext.AdjunctBatchAdjuncts.AsNoTracking().SingleAsync(a => a.BatchId == batchId);
        junctionRecord.AdjunctId.Should().Be("adj-1");
        junctionRecord.Status.Should().Be(DLCS.Model.Assets.BatchStatus.Completed);

        A.CallTo(() => IngestNotificationSender.SendIngestAdjunctRequest(
                A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => BatchCompletedNotificationSender.SendBatchCompletedMessage(
                A<DLCS.Model.Assets.AdjunctBatch>.That.Matches(b => b.Id == batchId), A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
    }

    [Fact]
    public async Task PostAdjunctBatch_Returns201_WithBatch_ForSingleHostedAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => IngestNotificationSender.SendIngestAdjunctRequest(
                A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>._, A<CancellationToken>._))
            .ReturnsLazily(_ => Task.FromResult(1));

        var json = $$"""
                     {
                       "member": [{
                         "id": "adj-hosted-1",
                         "asset": "{{assetId}}",
                         "@type": "Image",
                         "mediaType": "image/jpeg",
                         "iiifLink": "seeAlso",
                         "origin": "https://example.com/source.jpg"
                       }]
                     }
                     """;

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer)
            .PostAsync($"/customers/{assetId.Customer}/adjunctQueue",
                new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var batch = await response.ReadAsHydraResponseAsync<AdjunctBatch>();
        batch.Count.Should().Be(1);
        batch.Completed.Should().Be(0, "hosted adjunct is not yet complete");
        batch.Finished.Should().BeNull("batch is not finished until engine processes the adjunct");

        var batchId = ParseBatchId(batch);
        var junctionRecord = await dbContext.AdjunctBatchAdjuncts.AsNoTracking().SingleAsync(a => a.BatchId == batchId);
        junctionRecord.AdjunctId.Should().Be("adj-hosted-1");
        junctionRecord.Status.Should().Be(DLCS.Model.Assets.BatchStatus.Waiting);

        A.CallTo(() => IngestNotificationSender.SendIngestAdjunctRequest(
                A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>.That.Matches(a => a.Single().Id == "adj-hosted-1"),
                A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        A.CallTo(() => BatchCompletedNotificationSender.SendBatchCompletedMessage(
                A<DLCS.Model.Assets.AdjunctBatch>.That.Matches(b => b.Id == batchId), A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task PostAdjunctBatch_Returns201_WithMixedExternalAndHosted_CountsCorrect()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => IngestNotificationSender.SendIngestAdjunctRequest(
                A<IReadOnlyList<DLCS.Model.Assets.Adjunct>>._, A<CancellationToken>._))
            .ReturnsLazily(call => Task.FromResult(
                ((IReadOnlyList<DLCS.Model.Assets.Adjunct>)call.Arguments[0]!).Count));

        var json = $$"""
                     {
                       "member": [
                         {
                           "id": "adj-ext",
                           "asset": "{{assetId}}",
                           "@type": "Image",
                           "mediaType": "image/jpeg",
                           "iiifLink": "seeAlso",
                           "externalId": "https://example.com/ext.jpg"
                         },
                         {
                           "id": "adj-hosted",
                           "asset": "{{assetId}}",
                           "@type": "Image",
                           "mediaType": "image/jpeg",
                           "iiifLink": "seeAlso",
                           "origin": "https://example.com/source.jpg"
                         }
                       ]
                     }
                     """;

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer)
            .PostAsync($"/customers/{assetId.Customer}/adjunctQueue",
                new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var batch = await response.ReadAsHydraResponseAsync<AdjunctBatch>();
        batch.Count.Should().Be(2);
        batch.Completed.Should().Be(1, "only the external adjunct completes immediately");
        batch.Finished.Should().BeNull("one hosted adjunct is still pending");

        var batchId = ParseBatchId(batch);
        var junctionRecords = await dbContext.AdjunctBatchAdjuncts.AsNoTracking()
            .Where(a => a.BatchId == batchId)
            .ToListAsync();
        junctionRecords.Should().HaveCount(2);
        junctionRecords.Single(a => a.AdjunctId == "adj-ext").Status.Should().Be(DLCS.Model.Assets.BatchStatus.Completed);
        junctionRecords.Single(a => a.AdjunctId == "adj-hosted").Status.Should().Be(DLCS.Model.Assets.BatchStatus.Waiting);
        A.CallTo(() => BatchCompletedNotificationSender.SendBatchCompletedMessage(
                A<DLCS.Model.Assets.AdjunctBatch>.That.Matches(b => b.Id == batchId), A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task PostAdjunctBatch_Returns201_UpsertsExistingAdjunct()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct("adj-existing", externalId: "https://example.com/old.jpg");
        await dbContext.SaveChangesAsync();

        var json = $$"""
                     {
                       "member": [{
                         "id": "adj-existing",
                         "asset": "{{assetId}}",
                         "@type": "Image",
                         "mediaType": "image/png",
                         "iiifLink": "seeAlso",
                         "externalId": "https://example.com/new.jpg"
                       }]
                     }
                     """;

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer)
            .PostAsync($"/customers/{assetId.Customer}/adjunctQueue",
                new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var batchId = ParseBatchId(await response.ReadAsHydraResponseAsync<AdjunctBatch>());

        var updatedAdjunct = await dbContext.Adjuncts.AsNoTracking()
            .SingleAsync(a => a.AssetId == assetId && a.Id == "adj-existing");
        updatedAdjunct.ExternalId.Should().Be(new Uri("https://example.com/new.jpg"));
        updatedAdjunct.MediaType.Should().Be("image/png");
        updatedAdjunct.Batch.Should().Be(batchId);
    }

    [Fact]
    public async Task PostAdjunctBatch_Returns201_AdjunctHasBatchIdSet()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();

        var json = $$"""
                     {
                       "member": [{
                         "id": "adj-batch-fk",
                         "asset": "{{assetId}}",
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
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var batchId = ParseBatchId(await response.ReadAsHydraResponseAsync<AdjunctBatch>());

        var adjunct = await dbContext.Adjuncts.AsNoTracking()
            .SingleAsync(a => a.AssetId == assetId && a.Id == "adj-batch-fk");
        adjunct.Batch.Should().Be(batchId, "adjunct should reference the created batch");
    }

    [Fact]
    public async Task GetAdjunctBatch_Returns404_WhenBatchNotFound()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer)
            .GetAsync($"/customers/{assetId.Customer}/adjunctQueue/batches/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAdjunctBatch_Returns404_WhenBatchBelongsToDifferentCustomer()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();

        // Create a batch owned by a different customer directly in DB
        var otherCustomerBatch = new DLCS.Model.Assets.AdjunctBatch
        {
            Customer = assetId.Customer + 1,
            Submitted = DateTime.UtcNow,
            Count = 0,
            Completed = 0,
            Errors = 0
        };
        dbContext.AdjunctBatches.Add(otherCustomerBatch);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer)
            .GetAsync($"/customers/{assetId.Customer}/adjunctQueue/batches/{otherCustomerBatch.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAdjunctBatch_Returns200_WithBatchDetails()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();

        var json = $$"""
                     {
                       "member": [{
                         "id": "adj-get-test",
                         "asset": "{{assetId}}",
                         "@type": "Image",
                         "mediaType": "image/jpeg",
                         "iiifLink": "seeAlso",
                         "externalId": "https://example.com/adj"
                       }]
                     }
                     """;

        var postResponse = await httpClient.AsCustomer(assetId.Customer)
            .PostAsync($"/customers/{assetId.Customer}/adjunctQueue",
                new StringContent(json, Encoding.UTF8, "application/json"));
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdBatch = await postResponse.ReadAsHydraResponseAsync<AdjunctBatch>();
        var batchId = ParseBatchId(createdBatch);

        // Act
        var response = await httpClient.AsCustomer(assetId.Customer)
            .GetAsync($"/customers/{assetId.Customer}/adjunctQueue/batches/{batchId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var batch = await response.ReadAsHydraResponseAsync<AdjunctBatch>();
        batch.Count.Should().Be(createdBatch.Count);
        batch.Completed.Should().Be(createdBatch.Completed);
        batch.Errors.Should().Be(createdBatch.Errors);
        batch.Finished.Should().BeCloseTo(createdBatch.Finished.Value, TimeSpan.FromSeconds(2));
        ParseBatchId(batch).Should().Be(batchId);
    }

    /// <summary>
    /// Get the batch Id from the JSON-LD @id URL, e.g. ".../adjunctQueue/batches/42" → 42.
    /// ModelId is [JsonIgnore] so cannot be read directly from the deserialized response.
    /// </summary>
    private static int ParseBatchId(AdjunctBatch batch)
        => batch.GetLastPathElementAsInt()!.Value;
}
