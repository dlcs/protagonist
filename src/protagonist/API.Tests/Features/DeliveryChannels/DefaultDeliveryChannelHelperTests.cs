using API.Features.DeliveryChannels.Helpers;

namespace API.Tests.Features.DeliveryChannels;

public class DefaultDeliveryChannelHelperTests
{
    [Fact]
    public void GetSpaceZeroErrorMessage_ReturnsNull_WhenSpaceIsNull()
    {
        var result = DefaultDeliveryChannelHelper.GetSpaceZeroErrorMessage(null);
        result.Should().BeNull();
    }

    [Fact]
    public void GetSpaceZeroErrorMessage_ReturnsNull_WhenSpaceIsNonZero()
    {
        var result = DefaultDeliveryChannelHelper.GetSpaceZeroErrorMessage(5);
        result.Should().BeNull();
    }
}
