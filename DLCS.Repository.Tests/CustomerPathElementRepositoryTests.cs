using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using DLCS.Repository.Caching;
using DLCS.Repository.Settings;
using FakeItEasy;
using FluentAssertions;
using LazyCache;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DLCS.Repository.Tests
{
    public class CustomerPathElementRepositoryTests
    {
        private readonly ICustomerRepository customerRepository;
        private readonly ILogger<CustomerPathElementRepository> logger;
        private readonly IAppCache appCache;
        private readonly CustomerPathElementRepository sut;
        private const int CustomerId = 3;
        private const string CustomerName = "Robert-Paulson";
        
        public CustomerPathElementRepositoryTests()
        {
            customerRepository = A.Fake<ICustomerRepository>();
            A.CallTo(() => customerRepository.GetCustomerIdLookup())
                .Returns(new Dictionary<string, int> {[CustomerName] = CustomerId});
            
            logger = A.Fake<ILogger<CustomerPathElementRepository>>();
            appCache = new MockCachingService();

            sut = new CustomerPathElementRepository(appCache, Options.Create(new CacheSettings()), customerRepository,
                logger);
        }
        
        [Fact]
        public async Task GetCustomer_ById_ReturnsPathElement()
        {
            // Arrange
            var expected = new CustomerPathElement(CustomerId, CustomerName);

            // Act
            var byId = await sut.GetCustomer(CustomerId.ToString());

            // Assert
            byId.Should().BeEquivalentTo(expected);
        }
        
        [Fact]
        public void GetCustomer_ById_ThrowsIfNotFound()
        {
            // Act
            Func<Task<CustomerPathElement>> action = () => sut.GetCustomer($"not{CustomerId.ToString()}");

            // Assert
            action.Should().ThrowAsync<KeyNotFoundException>();
        }
        
        [Fact]
        public async Task GetCustomer_ByName_ReturnsPathElement()
        {
            // Arrange
            var expected = new CustomerPathElement(CustomerId, CustomerName);

            // Act
            var byId = await sut.GetCustomer(CustomerName);

            // Assert
            byId.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void GetCustomer_ByName_ThrowsIfNotFound()
        {
            // Act
            Func<Task<CustomerPathElement>> action = () => sut.GetCustomer($"not{CustomerName}");

            // Assert
            action.Should().ThrowAsync<KeyNotFoundException>();
        }
    }
}