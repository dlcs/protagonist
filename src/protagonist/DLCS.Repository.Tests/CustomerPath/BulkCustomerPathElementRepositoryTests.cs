using System;
using System.Collections.Generic;
using DLCS.Core.Caching;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using DLCS.Repository.CustomerPath;
using FakeItEasy;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Tests.CustomerPath;

public class BulkCustomerPathElementRepositoryTests
{
    private readonly BulkCustomerPathElementRepository sut;
    private const int CustomerId = 3;
    private const string CustomerName = "Robert-Paulson";
    
    public BulkCustomerPathElementRepositoryTests()
    {
        var customerRepository = A.Fake<ICustomerRepository>();
        A.CallTo(() => customerRepository.GetCustomerIdLookup())
            .Returns(new Dictionary<string, int> {[CustomerName] = CustomerId});
        
        var appCache = new MockCachingService();

        sut = new BulkCustomerPathElementRepository(appCache, Options.Create(new CacheSettings()), customerRepository,
            new NullLogger<BulkCustomerPathElementRepository>());
    }
    
    [Fact]
    public async Task GetCustomerPathElement_ById_ReturnsPathElement()
    {
        // Arrange
        var expected = new CustomerPathElement(CustomerId, CustomerName);

        // Act
        var byId = await sut.GetCustomerPathElement(CustomerId.ToString());

        // Assert
        byId.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void GetCustomerPathElement_ById_ThrowsIfNotFound()
    {
        // Act
        Func<Task<CustomerPathElement>> action = () => sut.GetCustomerPathElement($"not{CustomerId.ToString()}");

        // Assert
        action.Should().ThrowAsync<KeyNotFoundException>();
    }
    
    [Theory]
    [InlineData("Robert-Paulson")]
    [InlineData("robert-paulson")]
    [InlineData("ROBERT-PAULSON")]
    public async Task GetCustomerPathElement_ByName_AnyCase_ReturnsPathElement(string customerName)
    {
        // Arrange
        var expected = new CustomerPathElement(CustomerId, customerName);

        // Act
        var byId = await sut.GetCustomerPathElement(customerName);

        // Assert
        byId.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GetCustomerPathElement_ByName_ThrowsIfNotFound()
    {
        // Act
        Func<Task<CustomerPathElement>> action = () => sut.GetCustomerPathElement($"not{CustomerName}");

        // Assert
        action.Should().ThrowAsync<KeyNotFoundException>();
    }
}