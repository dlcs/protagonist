using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Repository;
using Hydra.Collections;
using Microsoft.EntityFrameworkCore;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class CustomHeaderTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dlcsContext;

    public CustomHeaderTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dlcsContext = dbFixture.DbContext;
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task Get_CustomHeader_200()
    {
        // Arrange
        const int customerId = 90;
        var path = $"customers/{customerId}/customHeaders";
        var customHeader = new CustomHeader()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Key = "test-key",
            Value = "test-value"
        };
        
        await dlcsContext.CustomHeaders.AddAsync(customHeader);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(Path.Combine(path, customHeader.Id));
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task Get_CustomHeader_404_IfNotFound()
    {
        // Arrange
        const int customerId = 91;
        var path = $"customers/{customerId}/customHeaders/{Guid.Empty}";
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Delete_CustomHeader_204()
    {
        // Arrange
        const int customerId = 92;
        var path = $"customers/{customerId}/customHeaders";
        var customHeader = new CustomHeader()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Key = "test-key",
            Value = "test-value"
        };
        
        await dlcsContext.CustomHeaders.AddAsync(customHeader);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).DeleteAsync(Path.Combine(path, customHeader.Id));
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var deletedNamedQuery = await dlcsContext.NamedQueries.AnyAsync(nq => nq.Id == customHeader.Id);
        deletedNamedQuery.Should().BeFalse();
    }
    
    [Fact]
    public async Task Get_CustomHeaders_200()
    {
        // Arrange
        const int customerId = 93;
        var path = $"customers/{customerId}/customHeaders";
        
        await dlcsContext.CustomHeaders.AddTestCustomHeader("test-key-1", "test-value-1", customerId);
        await dlcsContext.CustomHeaders.AddTestCustomHeader("test-key-2", "test-value-2", customerId, space:1);
        await dlcsContext.CustomHeaders.AddTestCustomHeader("test-key-3", "test-value-3", 1);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<CustomHeader>>();
        model.Members.Should().HaveCount(2);
    }
}