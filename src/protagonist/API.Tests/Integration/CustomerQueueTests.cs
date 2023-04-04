using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository;
using DLCS.Repository.Messaging;
using FakeItEasy;
using Hydra;
using Hydra.Collections;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;
using Batch = DLCS.Model.Assets.Batch;
using CustomerQueue = DLCS.Model.Processing.CustomerQueue;
using Queue = DLCS.Model.Processing.Queue;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class CustomerQueueTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    private static readonly IEngineClient EngineClient = A.Fake<IEngineClient>();
    
    public CustomerQueueTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                services.AddScoped<IEngineClient>(_ => EngineClient);
                services.AddAuthentication("API-Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "API-Test", _ => { });
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        dbFixture.CleanUp();

        // Setup batches for testing
        dbContext.Batches.AddRange(
            // finished today
            new Batch
            {
                Id = 4000, Customer = 99, Submitted = DateTime.UtcNow, Count = 5, Completed = 5,
                Finished = DateTime.UtcNow
            }, 
            // superseded
            new Batch
            {
                Id = 4001, Customer = 99, Submitted = DateTime.UtcNow, Count = 5, Completed = 5, Superseded = true
            },
            // all "completed" but not finished
            new Batch { Id = 4002, Customer = 99, Submitted = DateTime.UtcNow, Count = 10, Completed = 10, },
            // active - 5 left
            new Batch { Id = 4003, Customer = 99, Submitted = DateTime.UtcNow, Count = 10, Completed = 5, },
            // active - 2 errors, 2 done
            new Batch { Id = 4004, Customer = 99, Submitted = DateTime.UtcNow, Count = 8, Completed = 2, Errors = 2 },
            // finished last week
            new Batch
            {
                Id = 4005, Customer = 99, Submitted = DateTime.UtcNow, Count = 5, Completed = 5,
                Finished = DateTime.UtcNow.AddDays(-7)
            }, 
            // finished yesterday
            new Batch
            {
                Id = 4006, Customer = 99, Submitted = DateTime.UtcNow, Count = 5, Completed = 5,
                Finished = DateTime.UtcNow.AddDays(-1)
            }
        );
        dbContext.SaveChanges();
    }

    [Fact]
    public async Task Get_CustomerQueue_404_IfCustomerQueueNotFound()
    {
        // Arrange
        await dbContext.Customers.AddTestCustomer(-1);
        await dbContext.SaveChangesAsync();
        const string path = "customers/-1/queue";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_CustomerQueue_200_IfCustomerQueueFound_NoBatches()
    {
        // Arrange
        var customer = -2;
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Queues.AddAsync(new Queue { Customer = customer, Name = "default", Size = 10 });
        await dbContext.SaveChangesAsync();
        var path = $"customers/{customer}/queue";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<CustomerQueue>();
        queue.Size.Should().Be(10);
        queue.ImagesWaiting.Should().Be(0);
        queue.BatchesWaiting.Should().Be(0);
    }
    
    [Fact]
    public async Task Get_CustomerQueue_200_IfCustomerQueueFound_WithBatches()
    {
        // Arrange
        var customer = -3;
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Queues.AddAsync(new Queue { Customer = customer, Name = "default", Size = 10 });
        await dbContext.Batches.AddTestBatch(1, customer, count: 5, completed: 5, superseded: false); // 0
        await dbContext.Batches.AddTestBatch(2, customer, count: 5, completed: 0, superseded: false); // 5
        await dbContext.Batches.AddTestBatch(3, customer, count: 100, completed: 0, superseded: true); // superseded- ignored
        await dbContext.SaveChangesAsync();
        var path = $"customers/{customer}/queue";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<CustomerQueue>();
        queue.Size.Should().Be(10);
        queue.ImagesWaiting.Should().Be(5);
        queue.BatchesWaiting.Should().Be(2);
    }
    
    [Fact]
    public async Task Get_ActiveBatches_200_IfNoBatchesFound()
    {
        // Arrange
        var customer = -4;
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.SaveChangesAsync();
        var path = $"customers/{customer}/queue/active";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Batch>>();
        queue.Members.Should().BeEmpty();
    }
    
    [Fact]
    public async Task Get_ActiveBatches_200_IfIncludesActiveBatches()
    {
        // Arrange
        const string path = "customers/99/queue/active";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Batch>>();
        queue.Members.Should().HaveCount(3);
    }
    
    [Fact]
    public async Task Get_ActiveBatches_200_SupportsPaging()
    {
        // Arrange
        const string path = "customers/99/queue/active?pageSize=2";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Batch>>();
        queue.Members.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task Get_RecentBatches_200_IfNoBatchesFound()
    {
        // Arrange
        var customer = -4;
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.SaveChangesAsync();
        var path = $"customers/{customer}/queue/recent";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Batch>>();
        queue.Members.Should().BeEmpty();
    }
    
    [Fact]
    public async Task Get_RecentBatches_200_BatchesOrdered()
    {
        // Arrange
        const string path = "customers/99/queue/recent";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Batch>>();
        queue.Members.Should().HaveCount(3);
        queue.Members.Select(b => b.Id.GetLastPathElementAsInt()).Should().ContainInOrder(4000, 4006, 4005);
    }
    
    [Fact]
    public async Task Get_RecentBatches_200_SupportsPaging()
    {
        // Arrange
        const string path = "customers/99/queue/recent?pageSize=2&page=2";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Batch>>();
        queue.Members.Should().HaveCount(1);
        queue.Members.Single().Id.GetLastPathElementAsInt().Should().Be(4005);
    }
    
    [Fact]
    public async Task Get_Batches_200_IfNoBatchesFound()
    {
        // Arrange
        var customer = -5;
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.SaveChangesAsync();
        var path = $"customers/{customer}/queue/batches";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Batch>>();
        queue.Members.Should().BeEmpty();
    }
    
    [Fact]
    public async Task Get_Batches_200_BatchesOrdered()
    {
        // Arrange
        const string path = "customers/99/queue/batches";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Batch>>();
        queue.Members.Should().HaveCount(7);
        queue.Members.Select(b => b.Id.GetLastPathElementAsInt())
            .Should().ContainInOrder(4000, 4001, 4002, 4003, 4004, 4005, 4006);
    }
    
    [Fact]
    public async Task Get_Batches_200_SupportsPaging()
    {
        // Arrange
        const string path = "customers/99/queue/batches?pageSize=2&page=2";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Batch>>();
        queue.Members.Should().HaveCount(2);
        queue.Members.Select(b => b.Id.GetLastPathElementAsInt())
            .Should().ContainInOrder(4002, 4003);
    }

    [Fact]
    public async Task Get_Batch_404_IfNotFound()
    {
        // Arrange
        const string path = "customers/99/queue/batches/-2";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_Batch_200_IfFound()
    {
        // Arrange
        const string path = "customers/99/queue/batches/4004";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var batch = await response.ReadAsHydraResponseAsync<DLCS.HydraModel.Batch>();
        batch.Count.Should().Be(8);
        batch.Completed.Should().Be(2);
        batch.Errors.Should().Be(2);
    }
    
    [Fact]
    public async Task Get_CustomerPriorityQueue_404_IfCustomerQueueNotFound()
    {
        // Arrange
        await dbContext.Customers.AddTestCustomer(-1);
        await dbContext.SaveChangesAsync();
        const string path = "customers/-1/queue/priority";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_CustomerPriorityQueue_200_IfCustomerQueueFound_NoBatches()
    {
        // Arrange
        var customer = -6;
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Queues.AddAsync(new Queue { Customer = customer, Name = "priority", Size = 10 });
        await dbContext.SaveChangesAsync();
        var path = $"customers/{customer}/queue/priority";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<CustomerQueue>();
        queue.Size.Should().Be(10);
        queue.ImagesWaiting.Should().Be(0);
        queue.BatchesWaiting.Should().Be(0);
    }
    
    [Fact]
    public async Task Get_CustomerPriorityQueue_200_IfCustomerQueueFound_WithBatches()
    {
        // Arrange
        var customer = -7;
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Queues.AddAsync(new Queue { Customer = customer, Name = "priority", Size = 10 });
        await dbContext.Batches.AddTestBatch(1, customer, count: 5, completed: 5, superseded: false); // 0
        await dbContext.Batches.AddTestBatch(2, customer, count: 5, completed: 0, superseded: false); // 5
        await dbContext.Batches.AddTestBatch(3, customer, count: 100, completed: 0, superseded: true); // superseded- ignored
        await dbContext.SaveChangesAsync();
        var path = $"customers/{customer}/queue/priority";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var queue = await response.ReadAsHydraResponseAsync<CustomerQueue>();
        queue.Size.Should().Be(10);
        queue.ImagesWaiting.Should().Be(5);
        queue.BatchesWaiting.Should().Be(2);
    }
    
    [Fact]
    public async Task Get_Queue_200_DoesNotRequireAuth()
    {
        // Arrange
        const string path = "queue";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task Get_BatchImages_404_IfBatchNotFoundForCustomer()
    {
        // Arrange
        const string path = "customers/99/queue/batches/-1200/images";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_BatchImages_200_IfImagesFound()
    {
        // Arrange
        var idRoot = $"99/1/{nameof(Get_BatchImages_200_IfImagesFound)}";
        await dbContext.Images.AddTestAsset(AssetId.FromString($"{idRoot}1"), batch: 4006);
        await dbContext.Images.AddTestAsset(AssetId.FromString($"{idRoot}2"), batch: 4006);
        await dbContext.Images.AddTestAsset(AssetId.FromString($"{idRoot}3"), batch: 4006);
        await dbContext.SaveChangesAsync();
        
        // Not batch 4006 is added in ctor
        const string path = "customers/99/queue/batches/4006/images";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var images = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Image>>();
        images.TotalItems.Should().Be(3);
        images.Members.Should().HaveCount(3);
    }
    
    [Fact]
    public async Task Get_BatchImages_200_IfImagesFound_SupportsPaging()
    {
        // Arrange
        var idRoot = $"99/1/{nameof(Get_BatchImages_200_IfImagesFound_SupportsPaging)}";
        await dbContext.Images.AddTestAsset(AssetId.FromString($"{idRoot}1"), batch: 4005);
        await dbContext.Images.AddTestAsset(AssetId.FromString($"{idRoot}2"), batch: 4005);
        await dbContext.Images.AddTestAsset(AssetId.FromString($"{idRoot}3"), batch: 4005);
        await dbContext.SaveChangesAsync();
        
        const string path = "customers/99/queue/batches/4005/images?pageSize=2&page=2";

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var images = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Image>>();
        images.TotalItems.Should().Be(3);
        images.Members.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task Get_BatchImages_200_IfImagesFound_SupportsQuery()
    {
        // Arrange
        var idRoot = $"99/1/{nameof(Get_BatchImages_200_IfImagesFound_SupportsQuery)}";
        await dbContext.Images.AddTestAsset(AssetId.FromString($"{idRoot}1"), batch: 4004, num1: 10);
        await dbContext.Images.AddTestAsset(AssetId.FromString($"{idRoot}2"), batch: 4004, num1: 9);
        await dbContext.Images.AddTestAsset(AssetId.FromString($"{idRoot}3"), batch: 4004, num1: 10);
        await dbContext.SaveChangesAsync();
        
        var q = @"{""number1"":10}";
        var path = "customers/99/queue/batches/4004/images?q=" + q;

        // Act
        var response = await httpClient.AsCustomer().GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var images = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.Image>>();
        images.TotalItems.Should().Be(2);
        images.Members.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task Post_CreateBatch_400_IfValidationFails()
    {
        // Arrange
        var hydraImageBody = @"{
    ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
    ""@type"": ""Collection"",
    ""member"": [
        {
          ""id"": ""one"",
          ""origin"": ""https://example.org/vid.mp4"",
          ""space"": 1,
        }
    ]
}";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = "/customers/99/queue";

        // Act
        var response = await httpClient.AsCustomer(99).PostAsync(path, content);

        // Assert
        // status code correct
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_CreateBatch_400_IfSpaceNotFound()
    {
        // Arrange
        var hydraImageBody = @"{
    ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
    ""@type"": ""Collection"",
    ""member"": [
        {
          ""id"": ""one"",
          ""origin"": ""https://example.org/vid.mp4"",
          ""space"": 992,
          ""family"": ""T"",
          ""mediaType"": ""video/mp4""
        }
    ]
}";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = "/customers/99/queue";

        // Act
        var response = await httpClient.AsCustomer(99).PostAsync(path, content);

        // Assert
        // status code correct
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_CreateBatch_UpdatesQueueAndCounts()
    {
        // Arrange
        const int customerId = 1800;
        await dbContext.Customers.AddTestCustomer(customerId);
        await dbContext.Spaces.AddTestSpace(customerId, 2);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customerId);
        await dbContext.SaveChangesAsync();

        // a batch of 3: T, I and F resource
        var hydraImageBody = @"{
    ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
    ""@type"": ""Collection"",
    ""member"": [
        {
          ""id"": ""one"",
          ""origin"": ""https://example.org/vid.mp4"",
          ""space"": 2,
          ""family"": ""T"",
          ""mediaType"": ""video/mp4""
        },
        {
          ""id"": ""two"",
          ""origin"": ""https://example.org/image.jpg"",
          ""family"": ""I"",
          ""space"": 2,
          ""mediaType"": ""image/jpeg""
        },
        {
          ""id"": ""three"",
          ""origin"": ""https://example.org/file.pdf"",
          ""family"": ""F"",
          ""space"": 2,
          ""mediaType"": ""application/pdf""
        }
    ]
}";
        
        A.CallTo(() =>
            EngineClient.AsynchronousIngestBatch(
                A<IReadOnlyCollection<IngestAssetRequest>>._, false,
                A<CancellationToken>._)).Returns(3);
        
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        // status code correct
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        // Hydra batch received
        var hydraBatch = await response.ReadAsHydraResponseAsync<DLCS.HydraModel.Batch>();
        hydraBatch.Completed.Should().Be(0);
        hydraBatch.Count.Should().Be(3);
        var batchId = hydraBatch.GetLastPathElementAsInt()!.Value;
        
        // Db batch exists (unnecessary?)
        var dbBatch = await dbContext.Batches.SingleAsync(i => i.Customer == customerId);
        dbBatch.Id.Should().Be(batchId);

        // Images exist with Batch set + File marked as complete
        var images = dbContext.Images.Where(i => i.Customer == customerId && i.Space == 2).ToList();
        images.Count.Should().Be(3);
        images.Should().AllSatisfy(a =>
        {
            a.Finished.Should().BeNull();
            a.Ingesting.Should().BeTrue();
            a.Batch.Should().Be(batchId);
        });
        
        // Queue incremented
        var queue = await dbContext.Queues.SingleAsync(q => q.Customer == customerId && q.Name == "default");
        queue.Size.Should().Be(3);
        
        // Customer Storage incremented
        var storage = await dbContext.CustomerStorages.SingleAsync(q => q.Customer == customerId && q.Space == 0);
        storage.NumberOfStoredImages.Should().Be(3);

        // Items queued for processing
        A.CallTo(() =>
            EngineClient.AsynchronousIngestBatch(
                A<IReadOnlyCollection<IngestAssetRequest>>.That.Matches(i => i.Count == 3), false,
                A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task Post_CreatePriorityBatch_400_IfNonImage()
    {
        // Arrange
        var hydraImageBody = @"{
    ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
    ""@type"": ""Collection"",
    ""member"": [
        {
          ""id"": ""one"",
          ""origin"": ""https://example.org/vid.mp4"",
          ""space"": 2,
          ""family"": ""T"",
          ""mediaType"": ""video/mp4""
        },
        {
          ""id"": ""two"",
          ""origin"": ""https://example.org/image.jpg"",
          ""family"": ""I"",
          ""space"": 2,
          ""mediaType"": ""image/jpeg""
        },
        {
          ""id"": ""three"",
          ""origin"": ""https://example.org/file.pdf"",
          ""family"": ""F"",
          ""space"": 2,
          ""mediaType"": ""application/pdf""
        }
    ]
}";
        
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = "/customers/99/queue";

        // Act
        var response = await httpClient.AsCustomer(99).PostAsync(path, content);
        
        // Assert
        // status code correct
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_CreatePriorityBatch_UpdatesQueueAndCounts()
    {
        // Arrange
        const int customerId = 1900;
        await dbContext.Customers.AddTestCustomer(customerId);
        await dbContext.Spaces.AddTestSpace(customerId, 2);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customerId);
        await dbContext.SaveChangesAsync();

        // a batch of 3 images
        var hydraImageBody = @"{
    ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
    ""@type"": ""Collection"",
    ""member"": [
        {
          ""id"": ""one"",
          ""origin"": ""https://example.org/foo.jpg"",
          ""space"": 2,
          ""family"": ""I"",
          ""mediaType"": ""image/jpeg""
        },
        {
          ""id"": ""two"",
          ""origin"": ""https://example.org/foo.png"",
          ""family"": ""I"",
          ""space"": 2,
          ""mediaType"": ""image/png""
        },
        {
          ""id"": ""three"",
          ""origin"": ""https://example.org/foo.tiff"",
          ""family"": ""I"",
          ""space"": 2,
          ""mediaType"": ""image/tiff""
        }
    ]
}";
        
        A.CallTo(() =>
            EngineClient.AsynchronousIngestBatch(
                A<IReadOnlyCollection<IngestAssetRequest>>._, true,
                A<CancellationToken>._)).Returns(3);
        
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue/priority";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        // status code correct
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        // Hydra batch received
        var hydraBatch = await response.ReadAsHydraResponseAsync<DLCS.HydraModel.Batch>();
        hydraBatch.Completed.Should().Be(0);
        hydraBatch.Count.Should().Be(3);
        var batchId = hydraBatch.GetLastPathElementAsInt()!.Value;
        
        // Db batch exists (unnecessary?)
        var dbBatch = await dbContext.Batches.SingleAsync(i => i.Customer == customerId);
        dbBatch.Id.Should().Be(batchId);

        // Images exist with Batch set + File marked as complete
        var images = dbContext.Images.Where(i => i.Customer == customerId && i.Space == 2).ToList();
        images.Count.Should().Be(3);
        images.Should().AllSatisfy(a =>
        {
            a.Finished.Should().BeNull();
            a.Ingesting.Should().BeTrue();
            a.Batch.Should().Be(batchId);
        });

        // Queue incremented
        var queue = await dbContext.Queues.SingleAsync(q => q.Customer == customerId && q.Name == "priority");
        queue.Size.Should().Be(3);
        
        // Customer Storage incremented
        var storage = await dbContext.CustomerStorages.SingleAsync(q => q.Customer == customerId && q.Space == 0);
        storage.NumberOfStoredImages.Should().Be(3);

        // Items queued for processing
        A.CallTo(() =>
            EngineClient.AsynchronousIngestBatch(
                A<IReadOnlyCollection<IngestAssetRequest>>.That.Matches(i => i.Count == 3), true,
                A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task Post_TestBatch_404_IfBatchNotFoundForCustomer()
    {
        // Arrange
        const string path = "customers/99/queue/batches/-1200/test";

        // Act
        var response = await httpClient.AsCustomer().PostAsync(path, null!);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_TestBatch_MarksBatchAsSuperseded_IfNoImagesFound()
    {
        // Arrange
        await dbContext.Batches.AddTestBatch(201);
        await dbContext.SaveChangesAsync();
        const string path = "customers/99/queue/batches/201/test";

        // Act
        var response = await httpClient.AsCustomer().PostAsync(path, null!);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var dbBatch = await dbContext.Batches.SingleAsync(b => b.Id == 201);
        dbBatch.Superseded.Should().BeTrue();
    }
    
    [Fact]
    public async Task Post_TestBatch_MarksBatchAsComplete_IfImagesFoundAndAllFinished_AndBatchNotFinished()
    {
        // Arrange
        const int batch = 202;
        await dbContext.Batches.AddTestBatch(batch, count: 100);
        await dbContext.Images.AddTestAsset(AssetId.FromString("2/1/clown"), batch: batch, finished: DateTime.UtcNow);
        await dbContext.Images.AddTestAsset(AssetId.FromString("2/1/divine"), batch: batch, finished: DateTime.UtcNow);
        await dbContext.Images.AddTestAsset(AssetId.FromString("2/1/predictable"), batch: batch, finished: DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
        const string path = "customers/99/queue/batches/202/test";

        // Act
        var response = await httpClient.AsCustomer().PostAsync(path, null!);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var dbBatch = await dbContext.Batches.SingleAsync(b => b.Id == batch);
        dbBatch.Superseded.Should().BeFalse();
        dbBatch.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        dbBatch.Count.Should().Be(3);
    }

    [Fact]
    public async Task Post_TestBatch_ReturnsFalse_IfNotSupersededOrFinished()
    {
        // Arrange
        const int batch = 203;
        await dbContext.Batches.AddTestBatch(batch, count: 100);
        await dbContext.Images.AddTestAsset(AssetId.FromString("2/1/twist"), batch: batch, finished: DateTime.UtcNow);
        await dbContext.Images.AddTestAsset(AssetId.FromString("2/1/chi"), batch: batch, finished: DateTime.UtcNow);
        await dbContext.Images.AddTestAsset(AssetId.FromString("2/1/lost"), batch: batch, finished: null);
        await dbContext.SaveChangesAsync();
        const string path = "customers/99/queue/batches/203/test";

        // Act
        var response = await httpClient.AsCustomer().PostAsync(path, null!);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();

        var dbBatch = await dbContext.Batches.SingleAsync(b => b.Id == batch);
        dbBatch.Superseded.Should().BeFalse();
        dbBatch.Finished.Should().BeNull();
        dbBatch.Count.Should().Be(100);
    }
    
    [Fact]
    public async Task Post_TestBatch_DoesNotChangeBatchFinished_IfImagesFoundAndAllFinished_ButBatchHasFinishedDate()
    {
        // Arrange
        const int batch = 205;
        var finished = DateTime.UtcNow.AddDays(-3);
        await dbContext.Batches.AddTestBatch(batch, count: 13, finished: finished);
        await dbContext.Images.AddTestAsset(AssetId.FromString("2/1/donuts"), batch: batch, finished: DateTime.UtcNow);
        await dbContext.Images.AddTestAsset(AssetId.FromString("2/1/workinonit"), batch: batch, finished: DateTime.UtcNow);
        await dbContext.Images.AddTestAsset(AssetId.FromString("2/1/waves"), batch: batch, finished: DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
        const string path = "customers/99/queue/batches/205/test";

        // Act
        var response = await httpClient.AsCustomer().PostAsync(path, null!);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var dbBatch = await dbContext.Batches.SingleAsync(b => b.Id == batch);
        dbBatch.Superseded.Should().BeFalse();
        dbBatch.Finished.Should().BeCloseTo(finished, TimeSpan.FromMinutes((1)));
        dbBatch.Count.Should().Be(3);
    }
}