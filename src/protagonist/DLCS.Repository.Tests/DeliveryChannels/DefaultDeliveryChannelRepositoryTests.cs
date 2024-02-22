using System;
using System.Linq;
using DLCS.Core.Caching;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Policies;
using DLCS.Repository.DeliveryChannels;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.DeliveryChannels;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class DefaultDeliveryChannelRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly DefaultDeliveryChannelRepository sut;
    
    public DefaultDeliveryChannelRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        sut = new DefaultDeliveryChannelRepository(new MockCachingService(),new  NullLogger<DefaultDeliveryChannelRepository>(), 
            Options.Create(new CacheSettings()), dbFixture.DbContext);

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
           DeliveryChannelPolicyId = 1,
           MediaType = "image/*"
       });
       
       dbContext.SaveChanges();
    }

    [Fact]
    public void GetDefaultDeliveryChannelsForCustomer_ReturnsAllDefaultDeliveryChannels_WhenCalledWithSpaceWithoutSpecificChannels()
    {
        // Arrange and Act
        var channels = sut.GetDefaultDeliveryChannelsForCustomer(2, 1);

        // Assert
        channels.Count.Should().Be(1);
        channels.Count(x => x.DeliveryChannelPolicy.Channel == "iiif-img").Should().Be(1);
    }
    
    [Fact]
    public void GetDefaultDeliveryChannelsForCustomer_ReturnsAlls_WhenCalledWithSpaceWithSpecificChannels()
    {
        // Arrange and Act
        var channels = sut.GetDefaultDeliveryChannelsForCustomer(2, 2);

        // Assert
        channels.Count.Should().Be(2);
        channels.Count(x => x.DeliveryChannelPolicy.Channel == "iiif-img").Should().Be(2);
    }
    
    [Fact]
    public void GetDefaultDeliveryChannelsForCustomer_ReturnsNothing_WhenCalledWithInvalidCustomer()
    {
        // Arrange and Act
        var channels = sut.GetDefaultDeliveryChannelsForCustomer(3, 1);

        // Assert
        channels.Count.Should().Be(0);
    }
}