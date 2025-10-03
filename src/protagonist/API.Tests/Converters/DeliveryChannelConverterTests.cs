using API.Converters;
using API.Features.Image;
using DLCS.HydraModel;

namespace API.Tests.Converters;

public class DeliveryChannelConverterTests
{
    [Fact]
    public void ToInterimModel_ReturnsNullIfPassedNull() =>
        ((DeliveryChannel[])null).ToInterimModel().Should().BeNull();

    [Fact]
    public void ToInterimModel_ReturnsEmptyIfPassedEmpty() =>
        new DeliveryChannel[] { }.ToInterimModel().Should().BeEmpty();

    [Fact]
    public void ToInterimModel_ReturnsInterimModel()
    {
        var input = new DeliveryChannel[]
        {
            new() { Channel = "iiif-img", Id = "original", Policy = "use-original" },
            new() { Channel = "thumbs", Id = "default",  },
        };

        var expected = new DeliveryChannelsBeforeProcessing[]
        {
            new("iiif-img", "use-original"),
            new("thumbs", null)
        };

        input.ToInterimModel().Should().BeEquivalentTo(expected);
    }
}
