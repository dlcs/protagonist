using CleanupHandler;
using DLCS.Model.Assets;
using DLCS.Model.Policies;

namespace DeleteHandlerTests;

public class ChangeCalculatorTests
{
    private readonly ImageDeliveryChannel imageChannel = new()
    {
        Id = 1,
        Channel = AssetDeliveryChannels.Image,
        DeliveryChannelPolicy = new DeliveryChannelPolicy
        {
            Id = KnownDeliveryChannelPolicies.ImageDefault,
            Channel = AssetDeliveryChannels.Image,
            Modified = DateTime.UtcNow.AddDays(-1),
            Created = DateTime.MinValue
        },
        DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
    };
    
    private ImageDeliveryChannel fileChannel = new()
    {
        Id = 2,
        Channel = AssetDeliveryChannels.File,
        DeliveryChannelPolicy = new DeliveryChannelPolicy
        {
            Id = KnownDeliveryChannelPolicies.FileNone,
            Channel = AssetDeliveryChannels.File,
            Modified = DateTime.UtcNow.AddDays(-1),
            Created = DateTime.MinValue
        },
        DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
    };
    
    [Fact]
    public void GetChangeSets_ReturnsEmptyLists_IfSameBeforeAndAfter()
    {
        var before = new Asset
        {
            ImageDeliveryChannels = [imageChannel],
            Finished = DateTime.UtcNow,
        };
        var after = new Asset
        {
            ImageDeliveryChannels = [imageChannel],
            Finished = DateTime.UtcNow,
        };
        var (modifiedOrAddedChannels, removedChannels) = ChangeCalculator.GetChannelChangeSets(after, before);
        modifiedOrAddedChannels.Should().BeEmpty();
        removedChannels.Should().BeEmpty();
    }
    
    [Fact]
    public void GetChangeSets_ReturnsModifiedChannel_IfImageWasFinishedPriorToChannelModifiedDate()
    {
        // Image was finished 1 week ago, channel was modified 1 day ago, then ingested again  
        var before = new Asset
        {
            ImageDeliveryChannels = [imageChannel],
            Finished = DateTime.UtcNow.AddDays(-7)
        };
        var after = new Asset
        {
            ImageDeliveryChannels = [imageChannel],
            Finished = DateTime.UtcNow,
        };
        var (modifiedOrAddedChannels, removedChannels) = ChangeCalculator.GetChannelChangeSets(after, before);
        modifiedOrAddedChannels.Should().BeEquivalentTo([imageChannel]);
        removedChannels.Should().BeEmpty();
    }
    
    [Fact]
    public void GetChangeSets_ReturnsAddedChannelAndRemovedChannel()
    {
        var before = new Asset
        {
            ImageDeliveryChannels = [fileChannel],
            Finished = DateTime.UtcNow,
        };
        var after = new Asset
        {
            ImageDeliveryChannels = [imageChannel],
            Finished = DateTime.UtcNow,
        };
        var (modifiedOrAddedChannels, removedChannels) = ChangeCalculator.GetChannelChangeSets(after, before);
        modifiedOrAddedChannels.Should().BeEquivalentTo([imageChannel]);
        removedChannels.Should().BeEquivalentTo([fileChannel]);
    }

    [Fact]
    public void GetChangeSets_ReturnsAddedChannel()
    {
        var before = new Asset
        {
            ImageDeliveryChannels = [fileChannel],
            Finished = DateTime.UtcNow,
        };
        var after = new Asset
        {
            ImageDeliveryChannels = [imageChannel, fileChannel],
            Finished = DateTime.UtcNow,
        };
        var (modifiedOrAddedChannels, removedChannels) = ChangeCalculator.GetChannelChangeSets(after, before);
        modifiedOrAddedChannels.Should().BeEquivalentTo([imageChannel]);
        removedChannels.Should().BeEmpty();
    }
    
    [Fact]
    public void GetChangeSets_ReturnsRemovedChannel()
    {
        var before = new Asset
        {
            ImageDeliveryChannels = [imageChannel, fileChannel],
            Finished = DateTime.UtcNow,
        };
        var after = new Asset
        {
            ImageDeliveryChannels = [fileChannel],
            Finished = DateTime.UtcNow,
        };
        var (modifiedOrAddedChannels, removedChannels) = ChangeCalculator.GetChannelChangeSets(after, before);
        modifiedOrAddedChannels.Should().BeEmpty();
        removedChannels.Should().BeEquivalentTo([imageChannel]);
    }
}
