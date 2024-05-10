using System;
using System.Collections.Generic;
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
        var asset = new Asset { ImageDeliveryChannels = new List<ImageDeliveryChannel>() };

        asset.HasDeliveryChannel("anything").Should().BeFalse();
    }
    
    [Fact]
    public void HasDeliveryChannel_Throws_IfUnknown()
    {
        var asset = new Asset { ImageDeliveryChannels = new List<ImageDeliveryChannel>()
        {
            new()
            {
                Channel = AssetDeliveryChannels.Image
            }
        } };

        Action action = () => asset.HasDeliveryChannel("anything");

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
    
    [Theory]
    [InlineData(AssetDeliveryChannels.File)]
    [InlineData(AssetDeliveryChannels.Image)]
    [InlineData(AssetDeliveryChannels.Timebased)]
    public void HasDeliveryChannel_True(string channel)
    {
        var asset = new Asset { ImageDeliveryChannels = new List<ImageDeliveryChannel>()
        {
            new()
            {
                Channel = channel
            }
        } };

        asset.HasDeliveryChannel(channel).Should().BeTrue();
    }
    
    [Fact]
    public void HasSingleDeliveryChannel_False_IfChannelsNull()
    {
        var asset = new Asset();

        asset.HasSingleDeliveryChannel("anything").Should().BeFalse();
    }
    
    [Fact]
    public void HasSingleDeliveryChannel_False_IfChannelsEmpty()
    {
        var asset = new Asset { ImageDeliveryChannels = new List<ImageDeliveryChannel>() };

        asset.HasSingleDeliveryChannel("anything").Should().BeFalse();
    }

    [Theory]
    [InlineData(AssetDeliveryChannels.File)]
    [InlineData(AssetDeliveryChannels.Image)]
    [InlineData(AssetDeliveryChannels.Timebased)]
    public void HasSingleDeliveryChannel_False_IfContainsButNotSingle(string channel)
    {
        var asset = new Asset { ImageDeliveryChannels = new List<ImageDeliveryChannel>()};
        
        foreach (var deliveryChannel in AssetDeliveryChannels.All)
        {
            asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
            {
                Channel = deliveryChannel
            });
        }

        asset.HasSingleDeliveryChannel(channel).Should().BeFalse();
    }
    
    [Theory]
    [InlineData(AssetDeliveryChannels.File)]
    [InlineData(AssetDeliveryChannels.Image)]
    [InlineData(AssetDeliveryChannels.Timebased)]
    public void HasSingleDeliveryChannel_True(string channel)
    {
        var asset = new Asset { ImageDeliveryChannels = new List<ImageDeliveryChannel>()
        {
            new()
            {
                Channel = channel
            }
        } };
        
        asset.HasSingleDeliveryChannel(channel).Should().BeTrue();
    }
}