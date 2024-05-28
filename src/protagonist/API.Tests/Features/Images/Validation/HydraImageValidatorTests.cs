using System;
using API.Features.Image.Validation;
using API.Settings;
using DLCS.HydraModel;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;

namespace API.Tests.Features.Images.Validation;

public class HydraImageValidatorTests
{
    private readonly HydraImageValidator sut;

    public HydraImageValidatorTests()
    {
        var apiSettings = new ApiSettings() { RestrictedAssetIdCharacterString = "\\ "};
        sut = new HydraImageValidator(Options.Create(apiSettings));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MediaType_NullOrEmpty_OnCreate(string mediaType)
    {
        var model = new Image { MediaType = mediaType };
        var result = sut.TestValidate(model, options => options.IncludeRuleSets("default", "create"));
        result.ShouldHaveValidationErrorFor(a => a.MediaType);
    }
    
    [Fact]
    public void Batch_Provided()
    {
        var model = new Image { Batch = "10" };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.Batch);
    }
    
    [Fact]
    public void Width_Provided()
    {
        var model = new Image { Width = 10 };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Width)
            .WithErrorMessage("Should not include width");
    }

    [Fact]
    public void Height_Provided()
    {
        var model = new Image { Height = 10 };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Height)
            .WithErrorMessage("Should not include height");
    }
    
    [Fact]
    public void Duration_Provided()
    {
        var model = new Image { Duration = 10 };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Duration)
            .WithErrorMessage("Should not include duration");
    }
    
    [Fact]
    public void Finished_Provided()
    {
        var model = new Image { Finished = DateTime.Today };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.Finished);
    }
    
    [Fact]
    public void Created_Provided()
    {
        var model = new Image { Created = DateTime.Today };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.Created);
    }
    
    [Fact]
    public void DeliveryChannel_ValidationError_DeliveryChannelMissingChannel()
    {
        var apiSettings = new ApiSettings();
        var imageValidator = new HydraImageValidator(Options.Create(apiSettings));
        var model = new Image { DeliveryChannels = new[]
        {
            new DeliveryChannel()
            {
                Policy = "none"
            },
            new DeliveryChannel()
            {
                Channel = "file"
            }
        } };
        var result = imageValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.DeliveryChannels);
    }
    
    [Fact]
    public void DeliveryChannel_ValidationError_WhenNoneAndMoreDeliveryChannels()
    {
        var apiSettings = new ApiSettings();
        var imageValidator = new HydraImageValidator(Options.Create(apiSettings));
        var model = new Image { DeliveryChannels = new[]
        {
            new DeliveryChannel()
            {
                Channel = "none"
            },
            new DeliveryChannel()
            {
                Channel = "file"
            }
        } };
        var result = imageValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.DeliveryChannels);
    }
    
    [Fact]
    public void DeliveryChannel_NoValidationError_WhenDeliveryChannelsWithNoNone()
    {
        var apiSettings = new ApiSettings();
        var imageValidator = new HydraImageValidator(Options.Create(apiSettings));
        var model = new Image { DeliveryChannels = new[]
        {
            new DeliveryChannel()
            {
                Channel = "iiif-img"
            },
            new DeliveryChannel()
            {
                Channel = "file"
            }
        } };
        var result = imageValidator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.DeliveryChannels);
    }
    
    [Fact]
    public void DeliveryChannel_NoValidationError_WhenOnlyNone()
    {
        var apiSettings = new ApiSettings();
        var imageValidator = new HydraImageValidator(Options.Create(apiSettings));
        var model = new Image { DeliveryChannels = new[]
        {
            new DeliveryChannel()
            {
                Channel = "none"
            }
        } };
        var result = imageValidator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.DeliveryChannels);
    }
    
    [Theory]
    [InlineData("image/jpeg", "iiif-img")]
    [InlineData("image/jpeg", "thumbs")]  
    [InlineData("image/jpeg", "file")]
    [InlineData("image/jpeg", "none")]
    [InlineData("video/mp4", "iiif-av")]  
    [InlineData("video/mp4", "file")]  
    [InlineData("video/mp4", "none")]  
    [InlineData("audio/mp3", "iiif-av")] 
    [InlineData("audio/mp3", "file")] 
    [InlineData("audio/mp3", "none")] 
    [InlineData("application/pdf", "file")]
    [InlineData("application/pdf", "none")]
    public void DeliveryChannel_NoValidationError_WhenChannelValidForMediaType(string mediaType, string channel)
    {
        var apiSettings = new ApiSettings();
        var imageValidator = new HydraImageValidator(Options.Create(apiSettings));
        var model = new Image { 
            MediaType = mediaType,
            DeliveryChannels = new[]
            {
                new DeliveryChannel()
                {
                    Channel = channel,
                }
            } };
        var result = imageValidator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.DeliveryChannels);
    }
    
    [Theory]
    [InlineData("video/mp4", "iiif-img")]
    [InlineData("video/mp4", "thumbs")]
    [InlineData("image/jpeg", "iiif-av")]
    [InlineData("application/pdf", "iiif-img")]
    [InlineData("application/pdf", "thumbs")]
    [InlineData("application/pdf", "iiif-av")]
    public void DeliveryChannel_ValidationError_WhenWrongChannelForMediaType(string mediaType, string channel)
    {
        var apiSettings = new ApiSettings();
        var imageValidator = new HydraImageValidator(Options.Create(apiSettings));
        var model = new Image { 
            MediaType = mediaType,
            DeliveryChannels = new[]
        {
            new DeliveryChannel()
            {
                Channel = channel,
            }
        } };
        var result = imageValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.DeliveryChannels);
    }

    [Fact]
    public void DeliveryChannel_ValidationError_WhenEmpty_OnPatch()
    {
        var apiSettings = new ApiSettings();
        var imageValidator = new HydraImageValidator(Options.Create(apiSettings));
        var model = new Image
        {
            DeliveryChannels = Array.Empty<DeliveryChannel>()
        };
        var result = imageValidator.TestValidate(model, options => 
            options.IncludeRuleSets("default", "patch"));
        result.ShouldHaveValidationErrorFor(a => a.DeliveryChannels);
    }
}