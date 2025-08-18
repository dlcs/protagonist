using System;
using System.Linq;
using API.Features.DeliveryChannels.DataAccess;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Caching;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Policies;
using DLCS.Repository;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace API.Tests.Features.DeliveryChannels;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class DefaultDeliveryChannelRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly DefaultDeliveryChannelRepository sut;
    
    public DefaultDeliveryChannelRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        sut = new DefaultDeliveryChannelRepository(new MockCachingService(), new NullLogger<DefaultDeliveryChannelRepository>(), Options.Create(new CacheSettings()), dbFixture.DbContext);

        dbFixture.CleanUp();
        
        var newPolicy = dbContext.DeliveryChannelPolicies.Add(new DeliveryChannelPolicy()
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

       dbContext.DefaultDeliveryChannels.Add(new DefaultDeliveryChannel
        {
            Space = 2,
            Customer = 2,
            DeliveryChannelPolicyId = newPolicy.Entity.Id,
            MediaType = "image/tiff"
        });
       
       dbContext.DefaultDeliveryChannels.Add(new DefaultDeliveryChannel
       {
           Space = 0,
           Customer = 2,
           DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault,
           MediaType = "image/*"
       });
       
       dbContext.SaveChanges();
    }

    [Fact]
    public async Task MatchedDeliveryChannels_ReturnsAllDeliveryChannelPolicies_WhenCalled()
    {
        // Arrange and Act
        var matches = await sut.MatchedDeliveryChannels("image/tiff", 1, 2);

        // Assert
        matches.Count.Should().Be(1);
        matches.Count(x => x.Channel == "iiif-img").Should().Be(1);
    }
    
    [Fact]
    public async Task MatchedDeliveryChannels_ShouldNotMatchAnything_WhenCalledWithInvalidMediaType()
    {
        // Arrange and Act
        var matches = await sut.MatchedDeliveryChannels("notValid/tiff", 1, 2);

        // Assert
        matches.Count.Should().Be(0);
    }
    
    [Fact]
    public async Task MatchDeliveryChannelPolicyForChannel_MatchesDeliveryChannel_WhenMatchAvailable()
    {
        // Arrange and Act
        var matches = await sut.MatchDeliveryChannelPolicyForChannel("image/tiff", 1, 2, "iiif-img");

        // Assert
        matches.Should().NotBeNull();
    }
    
    [Fact]
    public async Task MatchDeliveryChannelPolicyForChannel_ThrowsException_WhenNotMatched()
    {
        // Arrange and Act
        Func<Task> action = () => sut.MatchDeliveryChannelPolicyForChannel("notMatched/tiff", 1, 2, "iiif-img");

        // Assert
        await action.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}