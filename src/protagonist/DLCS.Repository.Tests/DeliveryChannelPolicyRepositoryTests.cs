using System.Linq;
using DLCS.Core.Caching;
using DLCS.Repository.DeliveryChannels;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class DeliveryChannelPolicyRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly DeliveryChannelPolicyRepository sut;
    
    public DeliveryChannelPolicyRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbFixture.CleanUp();

        var cacheSettings = Options.Create(new CacheSettings());
        sut = new DeliveryChannelPolicyRepository(new MockCachingService(), new NullLogger<DeliveryChannelPolicyRepository>(), cacheSettings,
            dbContext);
    }

    [Fact]
    public async Task GetDeliveryChannelPolicy_ReturnsAPolicy()
    {
        // Arrange and Act
        var policy = await sut.GetDeliveryChannelPolicy(1, "default", "iiif-img");

        // Assert
        policy.Should().NotBeNull();
        policy.Channel.Should().Be("iiif-img");
        policy.Id.Should().Be(1);
    }
    
    [Fact]
    public async Task GetDeliveryChannelPolicy_ReturnsNull_WhenNoPolicyFound()
    {
        // Arrange and Act
        var policy = await sut.GetDeliveryChannelPolicy(1, "no-policy", "iiif-img");

        // Assert
        policy.Should().BeNull();
    }
    
    [Fact]
    public async Task AddDeliveryChannelPolicies_CreatesCorrectPolicies()
    {
        // Arrange and Act
        var policiesCreated = await sut.AddDeliveryChannelCustomerPolicies(100);

        var policies = dbContext.DeliveryChannelPolicies.Where(d => d.Customer == 100);

        // Assert
        policiesCreated.Should().BeTrue();
        policies.Count().Should().Be(3);
        policies.Should().ContainSingle(p => p.Channel == "thumbs");
        policies.Should().ContainSingle(p => p.Name == "default-audio");
        policies.Should().ContainSingle(p => p.Name == "default-video");
        policies.Should().NotContain(p => p.Channel == "iiif-img");
        policies.Should().NotContain(p => p.Channel == "file");
    }
}