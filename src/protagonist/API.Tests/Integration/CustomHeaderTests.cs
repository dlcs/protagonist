using System;
using System.Collections.Generic;
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
        var customHeader = new CustomHeader()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Key = "test-key",
            Value = "test-value"
        };
        var path = $"customers/{customerId}/customHeaders/{customHeader.Id}";
        
        await dlcsContext.CustomHeaders.AddAsync(customHeader);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        
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
        var customHeader = new CustomHeader()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Key = "test-key",
            Value = "test-value"
        };
        var path = $"customers/{customerId}/customHeaders/{customHeader.Id}";
        
        await dlcsContext.CustomHeaders.AddAsync(customHeader);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).DeleteAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var deletedCustomHeader = await dlcsContext.CustomHeaders.AnyAsync(ch => ch.Id == customHeader.Id);
        deletedCustomHeader.Should().BeFalse();
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
    
    [Fact]
    public async Task Post_CustomHeader_201()
    {
        // Arrange
        const int customerId = 94;
        var path = $"customers/{customerId}/customHeaders";
        
        const string newCustomHeaderJson = @"{
          ""key"": ""test-key"",
          ""value"": ""test-value"",
          ""space"": 1,
        }";
        
        // Act
        var content = new StringContent(newCustomHeaderJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var foundCustomHeader = dlcsContext.CustomHeaders.Single(ch => ch.Key == "test-key");
        foundCustomHeader.Should().NotBeNull();
        foundCustomHeader.Customer.Should().Be(customerId);
        foundCustomHeader.Value.Should().Be("test-value");
        foundCustomHeader.Space.Should().Be(1);
    }
    
    [Fact]
    public async Task Post_CustomHeader_201_IfMultipleSameKey()
    {
        // Arrange
        const int customerId = 95;
        const int customHeaderCount = 4;
        var responses = new List<HttpResponseMessage>();
        var path = $"customers/{customerId}/customHeaders";
        
        // Act
        for (var i = 0; i < customHeaderCount; i++)
        {
            var newCustomHeaderJson = $@"{{
              ""key"": ""same-key"",
              ""value"": ""test-value-{i}""
            }}";
            var content = new StringContent(newCustomHeaderJson, Encoding.UTF8, "application/json");
            var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
            responses.Add(response);
        }
        
        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode = HttpStatusCode.Created);
        dlcsContext.CustomHeaders.Where(ch => ch.Customer == customerId).Should().HaveCount(customHeaderCount);
    }

    [Fact]
    public async Task Post_CustomHeader_400IfKeyNotSpecified()
    {
        // Arrange
        const int customerId = 96;
        var path = $"customers/{customerId}/customHeaders";
        
        const string newCustomHeaderJson = @"{
          ""value"": ""test-value"",
          ""space"": 1,
        }";
        
        // Act
        var content = new StringContent(newCustomHeaderJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_CustomHeader_400IfValueNotSpecified()
    {
        // Arrange
        const int customerId = 97;
        var path = $"customers/{customerId}/customHeaders";
        
        const string newCustomHeaderJson = @"{
          ""key"": ""test-key"",
          ""space"": 1,
        }";
        
        // Act
        var content = new StringContent(newCustomHeaderJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_CustomHeader_200()
    {
        // Arrange
        const int customerId = 98;
        var customHeader = new CustomHeader()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Key = "test-key",
            Value = "test-value"
        };
        const string updatedCustomHeaderJson = @"{
          ""key"": ""test-key-2"",
          ""value"": ""test-value-2"",
          ""space"": 2,
        }";
        var path = $"customers/{customerId}/customHeaders/{customHeader.Id}";
        
        await dlcsContext.CustomHeaders.AddAsync(customHeader);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var content = new StringContent(updatedCustomHeaderJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var updatedCustomHeader = dlcsContext.CustomHeaders.Single(ch => ch.Key == "test-key-2");
        updatedCustomHeader.Should().NotBeNull();
        updatedCustomHeader.Value.Should().Be("test-value-2");
        updatedCustomHeader.Space.Should().Be(2);
    }
    
    [Fact]
    public async Task Put_CustomHeader_404_IfNotFound()
    {
        // Arrange
        const int customerId = 99;
        var path = $"customers/{customerId}/customHeaders/{Guid.Empty}";
        const string updatedCustomHeaderJson = @"{
          ""key"": ""test-key-2"",
          ""value"": ""test-value-2"",
          ""space"": 2,
        }";
        
        // Act
        var content = new StringContent(updatedCustomHeaderJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}