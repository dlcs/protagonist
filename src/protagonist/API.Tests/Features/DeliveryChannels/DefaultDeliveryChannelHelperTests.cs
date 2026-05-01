using API.Features.DeliveryChannels.Helpers;

namespace API.Tests.Features.DeliveryChannels;

public class DefaultDeliveryChannelHelperTests
{
    [Fact]
    public void GetSpaceZeroErrorMessage_ReturnsNull_WhenSpaceIsNull()
    {
        var result = DefaultDeliveryChannelHelper.GetSpaceZeroErrorMessage(null, SpaceZeroOperation.Create);
        result.Should().BeNull();
    }

    [Fact]
    public void GetSpaceZeroErrorMessage_ReturnsNull_WhenSpaceIsNonZero()
    {
        var result = DefaultDeliveryChannelHelper.GetSpaceZeroErrorMessage(5, SpaceZeroOperation.Create);
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(SpaceZeroOperation.Create, "create")]
    [InlineData(SpaceZeroOperation.Modify, "modify")]
    [InlineData(SpaceZeroOperation.Delete, "delete")]
    public void GetSpaceZeroErrorMessage_ReturnsError_WhenSpaceIsZero(SpaceZeroOperation operation, string expectedOpName)
    {
        var result = DefaultDeliveryChannelHelper.GetSpaceZeroErrorMessage(0, operation);
        result.Should().NotBeNull();
        result.Should().Contain("space 0");
        result.Should().Contain(expectedOpName);
    }
}