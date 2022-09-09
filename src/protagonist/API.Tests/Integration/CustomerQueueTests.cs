using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Assets;
using DLCS.Model.Processing;
using DLCS.Repository;
using Hydra;
using Hydra.Collections;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class CustomerQueueTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;    
    
    public CustomerQueueTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
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
        await dbContext.Batches.AddTestBatch(1, -3, count: 5, completed: 5, superseded: false); // 0
        await dbContext.Batches.AddTestBatch(2, -3, count: 5, completed: 0, superseded: false); // 5
        await dbContext.Batches.AddTestBatch(3, -3, count: 100, completed: 0, superseded: true); // superseded- ignored
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
}