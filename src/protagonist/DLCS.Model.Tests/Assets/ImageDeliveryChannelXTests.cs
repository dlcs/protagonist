using System;
using System.Collections.Generic;
using DLCS.Model.Assets;

namespace DLCS.Model.Tests.Assets;

public class ImageDeliveryChannelXTests
{
    [Fact]
    public void GetThumbsChannel_ThrowIfNotFoundFalse_ReturnsNull_IfListNull()
    {
        List<ImageDeliveryChannel> idcs = null;
        idcs.GetThumbsChannel(false).Should().BeNull();
    }
    
    [Fact]
    public void GetThumbsChannel_ThrowIfNotFoundFalse_ReturnsNull_IfListEmpty()
    {
        var idcs = new List<ImageDeliveryChannel>();
        idcs.GetThumbsChannel(false).Should().BeNull();
    }
    
    [Fact]
    public void GetThumbsChannel_ThrowIfNotFoundFalse_ReturnsNull_IfThumbsNotFound()
    {
        var idcs = new List<ImageDeliveryChannel> { new() { Channel = "iiif-img" } };
        idcs.GetThumbsChannel(false).Should().BeNull();
    }
    
    [Fact]
    public void GetThumbsChannel_ThrowIfNotFoundFalse_ReturnsThumbs()
    {
        var thumbsChannel = new ImageDeliveryChannel
        {
            Channel = "thumbs", DeliveryChannelPolicyId = 12354
        };
        var idcs = new List<ImageDeliveryChannel> { new() { Channel = "iiif-img" }, thumbsChannel };
        idcs.GetThumbsChannel(false).Should().Be(thumbsChannel);
    }
    
    [Fact]
    public void GetThumbsChannel_ThrowIfNotFoundTrue_Throws_IfListNull()
    {
        List<ImageDeliveryChannel> idcs = null;
        Action action = () => idcs.GetThumbsChannel(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
    
    [Fact]
    public void GetThumbsChannel_ThrowIfNotFoundTrue_Throws_IfListEmpty()
    {
        var idcs = new List<ImageDeliveryChannel>();
        Action action = () => idcs.GetThumbsChannel(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
    
    [Fact]
    public void GetThumbsChannel_ThrowIfNotFoundTrue_Throws_IfThumbsNotFound()
    {
        var idcs = new List<ImageDeliveryChannel> { new() { Channel = "iiif-img" } };
        Action action = () => idcs.GetThumbsChannel(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
    
    [Fact]
    public void GetThumbsChannel_ThrowIfNotFoundTrue_ReturnsThumbs()
    {
        var thumbsChannel = new ImageDeliveryChannel
        {
            Channel = "thumbs", DeliveryChannelPolicyId = 12354
        };
        var idcs = new List<ImageDeliveryChannel> { new() { Channel = "iiif-img" }, thumbsChannel };
        idcs.GetThumbsChannel(true).Should().Be(thumbsChannel);
    }
    
    [Fact]
    public void GetTimebasedChannel_ThrowIfNotFoundFalse_ReturnsNull_IfListNull()
    {
        List<ImageDeliveryChannel> idcs = null;
        idcs.GetTimebasedChannel(false).Should().BeNull();
    }
    
    [Fact]
    public void GetTimebasedChannel_ThrowIfNotFoundFalse_ReturnsNull_IfListEmpty()
    {
        var idcs = new List<ImageDeliveryChannel>();
        idcs.GetTimebasedChannel(false).Should().BeNull();
    }
    
    [Fact]
    public void GetTimebasedChannel_ThrowIfNotFoundFalse_ReturnsNull_IfTimebasedNotFound()
    {
        var idcs = new List<ImageDeliveryChannel> { new() { Channel = "iiif-img" } };
        idcs.GetTimebasedChannel(false).Should().BeNull();
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetTimebasedChannel_ReturnsTimebased_IfFound(bool throwIfNotFound)
    {
        var timebasedChannel = new ImageDeliveryChannel
        {
            Channel = "iiif-av", DeliveryChannelPolicyId = 12354
        };
        var idcs = new List<ImageDeliveryChannel> { new() { Channel = "iiif-img" }, timebasedChannel };
        idcs.GetTimebasedChannel(throwIfNotFound).Should().Be(timebasedChannel);
    }
    
    [Fact]
    public void GetTimebasedChannel_ThrowIfNotFoundTrue_Throws_IfListNull()
    {
        List<ImageDeliveryChannel> idcs = null;
        Action action = () => idcs.GetTimebasedChannel(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
    
    [Fact]
    public void GetTimebasedChannel_ThrowIfNotFoundTrue_Throws_IfListEmpty()
    {
        var idcs = new List<ImageDeliveryChannel>();
        Action action = () => idcs.GetTimebasedChannel(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
    
    [Fact]
    public void GetTimebasedChannel_ThrowIfNotFoundTrue_Throws_IfTimebasedNotFound()
    {
        var idcs = new List<ImageDeliveryChannel> { new() { Channel = "iiif-img" } };
        Action action = () => idcs.GetTimebasedChannel(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
}
