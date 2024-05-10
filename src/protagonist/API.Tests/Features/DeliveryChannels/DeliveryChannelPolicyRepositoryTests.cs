using System;
using API.Features.DeliveryChannels;
using API.Features.DeliveryChannels.DataAccess;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Caching;
using DLCS.Model.Policies;
using DLCS.Repository;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace API.Tests.Features.DeliveryChannels;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class DeliveryChannelPolicyRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly DeliveryChannelPolicyRepository sut;
    
    public DeliveryChannelPolicyRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        sut = new DeliveryChannelPolicyRepository(new MockCachingService(), new NullLogger<DeliveryChannelPolicyRepository>(), Options.Create(new CacheSettings()), dbFixture.DbContext);

        dbFixture.CleanUp();
        
        dbContext.DeliveryChannelPolicies.Add(new DeliveryChannelPolicy()
        {
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            DisplayName = "test policy - space specific",
            PolicyData = null,
            Name = "space-specific-image",
            Channel = "iiif-img",
            Customer = 2,
            Id = 260
        });

        dbContext.SaveChanges();
    }

    [Theory]
    [InlineData("space-specific-image")]
    [InlineData("channel/space-specific-image")]
    [InlineData("https://dlcs.api/customers/2/deliveryChannelPolicies/iiif-img/space-specific-image")]
    public async Task RetrieveDeliveryChannelPolicy_RetrievesACustomerSpecificPolicy(string policy)
    {
        // Arrange and Act
        var deliveryChannelPolicy = await sut.RetrieveDeliveryChannelPolicy(2, "iiif-img", policy);

        // Assert
        deliveryChannelPolicy.Channel.Should().Be("iiif-img");
        deliveryChannelPolicy.Customer.Should().Be(2);
    }

    [Fact]
    public async Task RetrieveDeliveryChannelPolicy_RetrievesADefaultPolicy()
    {
        // Arrange and Act
        var policy = await sut.RetrieveDeliveryChannelPolicy(2, "iiif-img", "default");

        // Assert
        policy.Channel.Should().Be("iiif-img");
        policy.Customer.Should().Be(1);
    }
    
    [Fact]
    public async Task RetrieveDeliveryChannelPolicy_RetrieveNonExistentPolicy()
    {
        // Arrange and Act
        Func<Task> action = () => sut.RetrieveDeliveryChannelPolicy(2, "notAChannel", "notAPolicy");

        // Assert
        await action.Should()
            .ThrowAsync<InvalidOperationException>();
    }
}