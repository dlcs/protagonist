using System;
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
        var model = new DefaultDeliveryChannel("", 1, "iiif-img", null, mediaType, Guid.NewGuid().ToString(), 0);
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.MediaType);
        result.ShouldNotHaveValidationErrorFor(a => a.Channel);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("someChannel")]
    public void Channel_NullOrEmpty(string channel)
    {
        var model = new DefaultDeliveryChannel("", 1, channel, null, "image/*", Guid.NewGuid().ToString(), 0);
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.Channel);
        result.ShouldNotHaveValidationErrorFor(a => a.MediaType);
    }
    
    [Fact]
    public void No_Errors()
    {
        var model = new DefaultDeliveryChannel("", 1, "iiif-img", null, "image/*", Guid.NewGuid().ToString(), 0);
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.Channel);
        result.ShouldNotHaveValidationErrorFor(a => a.MediaType);
    }
}