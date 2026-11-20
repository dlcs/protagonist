using System.Collections.Generic;
using DLCS.Core.Caching;
using DLCS.Repository.Customers;
using LazyCache.Mocks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Customers;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class DapperCustomerRepositoryTests
{
    private readonly DapperCustomerRepository sut;

    public DapperCustomerRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:PostgreSQLConnection"] = dbFixture.ConnectionString
            }).Build();

        sut = new DapperCustomerRepository(configuration, new MockCachingService(),
            Options.Create(new CacheSettings()));
    }

    [Theory]
    [InlineData("test")]
    [InlineData("Test")]
    [InlineData("TEST")]
    public async Task GetCustomer_ByName_IsCaseInsensitive(string name)
    {
        // Customer 99 "test" seeded by DlcsDatabaseFixture
        var customer = await sut.GetCustomer(name);

        customer.Should().NotBeNull();
        customer!.Id.Should().Be(99);
        customer.Name.Should().Be("test");
    }

    [Fact]
    public async Task GetCustomer_ByName_ReturnsNull_IfNotFound()
    {
        var customer = await sut.GetCustomer("not-a-customer");

        customer.Should().BeNull();
    }
}
