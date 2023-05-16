using DLCS.Core.Caching;
using DLCS.Model.Policies;
using DLCS.Repository.Policies;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Policies;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class PolicyRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly PolicyRepository sut;

    public PolicyRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbFixture.CleanUp();

        dbContext.ImageOptimisationPolicies.AddRange(
            new ImageOptimisationPolicy
            {
                Customer = 100, Global = false, Id = "the-default", Name = "Customer Default",
                TechnicalDetails = new[] { "foo" }
            }, new ImageOptimisationPolicy
            {
                Customer = 0, Global = true, Id = "the-default", Name = "Global Default",
                TechnicalDetails = new[] { "bar" }
            }, new ImageOptimisationPolicy
            {
                Customer = 999, Global = false, Id = "the-default", Name = "Other Customer Default",
                TechnicalDetails = new[] { "baz" }
            });
        dbContext.SaveChanges();

        var cacheSettings = Options.Create(new CacheSettings());
        sut = new PolicyRepository(new MockCachingService(), new NullLogger<PolicyRepository>(), cacheSettings,
            dbContext);
    }

    [Fact]
    public async Task GetImageOptimisationPolicy_ReturnsCustomerSpecific_IfDuplicate()
    {
        // Act
        var result = await sut.GetImageOptimisationPolicy("the-default", 100);
        
        // Assert
        result.Name.Should().Be("Customer Default");
    }
    
    [Fact]
    public async Task GetImageOptimisationPolicy_ReturnsGlobal_IfNoCustomerSpecific()
    {
        // Act
        var result = await sut.GetImageOptimisationPolicy("the-default", 1);
        
        // Assert
        result.Name.Should().Be("Global Default");
    }
}