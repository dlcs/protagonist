using System.Threading.Tasks;
using DLCS.Repository.Assets;
using DLCS.Repository.Caching;
using FluentAssertions;
using LazyCache.Mocks;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;
using Xunit;

namespace DLCS.Repository.Tests.Assets
{
    [Trait("Category", "Database")]
    [Collection(DatabaseCollection.CollectionName)]
    public class NamedQueryRepositoryTests
    {
        private readonly DlcsContext dbContext;
        private readonly NamedQueryRepository sut;

        public NamedQueryRepositoryTests(DlcsDatabaseFixture dbFixture)
        {
            dbContext = dbFixture.DbContext;
            sut = new NamedQueryRepository(dbFixture.DbContext, new MockCachingService(),
                Options.Create(new CacheSettings()));
            
            dbFixture.CleanUp();
            dbContext.NamedQueries.AddTestNamedQuery("global-and-local", 98, global: true);
            dbContext.NamedQueries.AddTestNamedQuery("global-and-local", 99, global: false);
            dbContext.NamedQueries.AddTestNamedQuery("different-customer", 98, global: false);
            dbContext.NamedQueries.AddTestNamedQuery("customer", 99, global: false);
            dbContext.SaveChanges();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetByName_Null_IfNoMatching(bool includeGlobal)
        {
            // Act
            var namedQuery = await sut.GetByName(99, "non-existant", includeGlobal);
            
            // Assert
            namedQuery.Should().BeNull();
        }
        
        [Fact]
        public async Task GetByName_Null_IfDifferentCustomer()
        {
            // Act
            var namedQuery = await sut.GetByName(99, "different-customer");
            
            // Assert
            namedQuery.Should().BeNull();
        }
        
        [Fact]
        public async Task GetByName_ReturnsExpected_NonGlobal()
        {
            // Act
            var namedQuery = await sut.GetByName(99, "customer");
            
            // Assert
            namedQuery.Name.Should().Be("customer");
            namedQuery.Customer.Should().Be(99);
        }

        [Fact]
        public async Task GetByName_ReturnsLocalFirst_IfMatchingGlobal()
        {
            // Act
            var namedQuery = await sut.GetByName(99, "global-and-local");
            
            // Assert
            namedQuery.Name.Should().Be("global-and-local");
            namedQuery.Customer.Should().Be(99);
        }
        
        [Fact]
        public async Task GetByName_ReturnsGlobal_IfIfNoLocal()
        {
            // Act
            var namedQuery = await sut.GetByName(3, "global-and-local");
            
            // Assert
            namedQuery.Name.Should().Be("global-and-local");
            namedQuery.Customer.Should().Be(98);
        }
    }
}