using System;
using DLCS.Model.Assets;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Assets;

public class AssetDeliveryChannelsTests
{
    [Fact]
    public void HasDeliveryChannel_False_IfChannelsNull()
    {
        var asset = new Asset();

        asset.HasDeliveryChannel("anything").Should().BeFalse();
    }
    
    [Fact]
    public void HasDeliveryChannel_False_IfChannelsEmpty()
    {
        var asset = new Asset { DeliveryChannel = Array.Empty<string>() };

        asset.HasDeliveryChannel("anything").Should().BeFalse();
    }
    
    [Fact]
    public void HasDeliveryChannel_Throws_IfUnknown()
    {
        var asset = new Asset { DeliveryChannel = new[] { "iiif-img" } };

        Action action = () => asset.HasDeliveryChannel("anything");

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
    
    [Theory]
    [InlineData(AssetDeliveryChannels.File)]
    [InlineData(AssetDeliveryChannels.Image)]
    [InlineData(AssetDeliveryChannels.Timebased)]
    [InlineData(AssetDeliveryChannels.Thumbs)]
    public void HasDeliveryChannel_True(string channel)
    {
        var asset = new Asset { DeliveryChannel = AssetDeliveryChannels.All };

        asset.HasDeliveryChannel(channel).Should().BeTrue();
    }
}