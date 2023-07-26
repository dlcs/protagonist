using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository;
using Hydra.Collections;
using Microsoft.EntityFrameworkCore;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class NamedQueryTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dlcsContext;

    public NamedQueryTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dlcsContext = dbFixture.DbContext;
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task Get_NamedQuery_200()
    {
        // Arrange
        const int customerId = 96;
        var path = $"customers/{customerId}/namedQueries";
        var namedQuery = new NamedQuery()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Name = "namedQueryTest",
            Template = "test",
            Global = false,
        };
        
        await dlcsContext.NamedQueries.AddAsync(namedQuery);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(Path.Combine(path, namedQuery.Id));
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task Get_NamedQuery_404_IfNotFound()
    {
        // Arrange
        const int customerId = 96;
        var path = $"customers/{customerId}/namedQueries/{Guid.Empty}";
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Delete_NamedQuery_204()
    {
        // Arrange
        const int customerId = 97;
        var path = $"customers/{customerId}/namedQueries";
        var namedQuery = new NamedQuery()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Name = "namedQueryTest",
            Template = "test",
            Global = false,
        };
        
        await dlcsContext.NamedQueries.AddAsync(namedQuery);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).DeleteAsync(Path.Combine(path, namedQuery.Id));
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var deletedNamedQuery = await dlcsContext.NamedQueries.AnyAsync(nq => nq.Id == namedQuery.Id);
        deletedNamedQuery.Should().BeFalse();
    }
    
    [Fact]
    public async Task Get_NamedQueries_200()
    {
        // Arrange
        const int customerId = 98;
        var path = $"customers/{customerId}/namedQueries";
        
        await dlcsContext.NamedQueries.AddTestNamedQuery("testNamedQuery1", customer: customerId, global: false);
        await dlcsContext.NamedQueries.AddTestNamedQuery("testNamedQuery2", customer: 1);
        await dlcsContext.NamedQueries.AddTestNamedQuery("testNamedQuery3", customer: 1, global: false);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<NamedQuery>>();
        model.Members.Should().HaveCount(2);
    }

    [Fact]
    public async Task Post_NamedQuery_201()
    {
        // Arrange
        const int customerId = 99;
        var path = $"customers/{customerId}/namedQueries";
        
        const string newNamedQueryJson = @"{
          ""name"": ""namedQueryTest"",
          ""template"": ""test"",
        }";
        
        // Act
        var content = new StringContent(newNamedQueryJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var foundNamedQuery = dlcsContext.NamedQueries.Single(nq => nq.Name == "namedQueryTest");
        foundNamedQuery.Template.Should().Be("test");
        foundNamedQuery.Customer.Should().Be(customerId);
        foundNamedQuery.Global.Should().BeFalse();
    }

    [Fact]
    public async Task Put_NamedQuery_200()
    {
        // Arrange
        const int customerId = 94;
        var path = $"customers/{customerId}/namedQueries";
        var namedQuery = new NamedQuery()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Name = "namedQueryTest",
            Template = "test",
            Global = false,
        };
        const string updatedNamedQueryJson = @"{
          ""template"": ""test-2"",
        }";
        
        await dlcsContext.NamedQueries.AddAsync(namedQuery);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var content = new StringContent(updatedNamedQueryJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(Path.Combine(path, namedQuery.Id), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedNamedQuery = await dlcsContext.NamedQueries.FirstOrDefaultAsync(nq => nq.Id == namedQuery.Id);
        updatedNamedQuery.Template.Should().Be("test-2");
    }
    
    [Fact]
    public async Task Put_NamedQuery_404_IfNotFound()
    {
        // Arrange
        const int customerId = 96;
        var path = $"customers/{customerId}/namedQueries/{Guid.Empty}";
        const string updatedNamedQueryJson = @"{
          ""template"": ""test-2"",
        }";
        
        // Act
        var content = new StringContent(updatedNamedQueryJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    
    [Fact]
    public async Task Post_NamedQuery_400_IfUserCreatesGlobal()
    {
        // Arrange
        const int customerId = 95;
        var path = $"customers/{customerId}/namedQueries";
        
        const string newNamedQueryJson = @"{
          ""name"": ""namedQueryTest"",
          ""template"": ""test"",
          ""global"": ""true""
        }";

        // Act
        var content = new StringContent(newNamedQueryJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}