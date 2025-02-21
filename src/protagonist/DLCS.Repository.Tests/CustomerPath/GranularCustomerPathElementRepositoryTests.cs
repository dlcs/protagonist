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

public class GranularCustomerPathElementRepositoryTests
{
    private readonly GranularCustomerPathElementRepository sut;
    private readonly ICustomerRepository customerRepository;
    private const int CustomerId = 3;
    private const string CustomerName = "Robert-Paulson";
    private readonly Customer customer = new() { Id = CustomerId, Name = CustomerName };
    
    public GranularCustomerPathElementRepositoryTests()
    {
        customerRepository = A.Fake<ICustomerRepository>();
        
        var appCache = new MockCachingService();

        sut = new GranularCustomerPathElementRepository(appCache, Options.Create(new CacheSettings()), customerRepository,
            new NullLogger<GranularCustomerPathElementRepository>());
    }
    
    [Fact]
    public async Task GetCustomerPathElement_ById_ReturnsPathElement()
    {
        // Arrange
        A.CallTo(() => customerRepository.GetCustomer(CustomerId)).Returns(customer);
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
        A.CallTo(() => customerRepository.GetCustomer(CustomerId.ToString())).Returns<Customer>(null);
        Func<Task<CustomerPathElement>> action = () => sut.GetCustomerPathElement(CustomerId.ToString());

        // Assert
        action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Customer 3 not found");
    }
    
    [Theory]
    [InlineData("Robert-Paulson")]
    [InlineData("robert-paulson")]
    [InlineData("ROBERT-PAULSON")]
    public async Task GetCustomerPathElement_ByName_AnyCase_ReturnsPathElement(string customerName)
    {
        // Arrange
        A.CallTo(() => customerRepository.GetCustomer(customerName)).Returns(customer);
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
        A.CallTo(() => customerRepository.GetCustomer(CustomerName)).Returns<Customer>(null);
        Func<Task<CustomerPathElement>> action = () => sut.GetCustomerPathElement(CustomerName);

        // Assert
        action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Customer Robert-Paulson not found");
    }
}