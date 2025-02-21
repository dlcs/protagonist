using System.Net;
using System.Net.Http;
using API.Client;
using DLCS.HydraModel;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
public class ApplicationTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dlcsContext;

    public ApplicationTests(ProtagonistAppFactory<Startup> factory)
    {
        var dbFixture = new DlcsDefaultDatabaseFixture();
        dbFixture.InitializeAsync().Wait();
        dlcsContext = dbFixture.DbContext;
        httpClient = factory.WithConnectionString(dbFixture.ConnectionString).CreateClient();
    }

    [Fact]
    public async Task SetupApplication_Fail_Customer1AlreadyExists()
    {
        // Arrange
        await dlcsContext.Customers.AddTestCustomer(1, "admin", "Admin");
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.PostAsync("/setup", null!);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
    
    [Fact]
    public async Task SetupApplication_Success_CreatesCustomerCounterAndStoragePolicy()
    {
        // Act
        var response = await httpClient.PostAsync("/setup", null!);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var apiKeyResponse = await response.ReadAsHydraResponseAsync<ApiKey>();
        
        var customer = await dlcsContext.Customers.SingleAsync(c => c.Id == 1);
        customer.Keys.Should().OnlyContain(s => s == apiKeyResponse.Key);
        (await dlcsContext.EntityCounters
                .AnyAsync(c => c.Scope == "1" && c.Customer == 1 && c.Type == "space"))
            .Should().BeTrue();
        
        // Note: -99 value comes from appsttings.Testing.json 
        var storagePolicy = await dlcsContext.StoragePolicies.SingleAsync(s => s.Id == "default");
        storagePolicy.MaximumNumberOfStoredImages.Should().Be(-99);
        storagePolicy.MaximumTotalSizeOfStoredImages.Should().Be(-99);
    }
}