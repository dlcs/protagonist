using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Processing;
using DLCS.Repository;
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
}