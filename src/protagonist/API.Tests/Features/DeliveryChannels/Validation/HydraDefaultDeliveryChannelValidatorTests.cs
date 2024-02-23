using API.Features.DeliveryChannels.Validation;
using DLCS.HydraModel;
using FluentValidation.TestHelper;

namespace API.Tests.Features.DeliveryChannels.Validation;

public class HydraDefaultDeliveryChannelValidatorTests
{
    private readonly HydraDefaultDeliveryChannelValidator sut;
    
    public HydraDefaultDeliveryChannelValidatorTests()
    {
        sut = new HydraDefaultDeliveryChannelValidator();
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("stuff/?")]
    public void MediaType_NullEmptyOrInvalid(string mediaType)
    {
        var model = new DefaultDeliveryChannel
        {
            MediaType = mediaType,
            Channel = "iiif-test"
        };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.MediaType);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Channel_NullOrEmpty(string channel)
    {
        var model = new DefaultDeliveryChannel
        {
            MediaType = "image/*",
            Channel = channel
        };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.Channel);
    }
}