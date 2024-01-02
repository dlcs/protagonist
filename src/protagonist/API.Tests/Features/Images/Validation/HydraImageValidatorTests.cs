using System;
using API.Features.Image.Validation;
using API.Settings;
using DLCS.Model.Policies;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;

namespace API.Tests.Features.Images.Validation;

public class HydraImageValidatorTests
{
    private readonly HydraImageValidator sut;

    public HydraImageValidatorTests()
    {
        var apiSettings = new ApiSettings() { DeliveryChannelsEnabled = true, RestrictedAssetIdCharacters = "\\ "};
        sut = new HydraImageValidator(Options.Create(apiSettings));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MediaType_NullOrEmpty(string mediaType)
    {
        var model = new DLCS.HydraModel.Image { MediaType = mediaType };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.MediaType);
    }
    
    [Fact]
    public void Batch_Provided()
    {
        var model = new DLCS.HydraModel.Image { Batch = "10" };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.Batch);
    }
    
    [Fact]
    public void Width_Provided()
    {
        var model = new DLCS.HydraModel.Image { Width = 10 };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Width)
            .WithErrorMessage("Should not include width");
    }
    
    [Theory]
    [InlineData("image/jpeg", "file,iiif-img")]
    [InlineData("video/mp4", "file,iiif-av")]
    [InlineData("audio/mp4", "file")]
    public void Width_Provided_NotFileOnly_OrAudio(string mediaType, string dc)
    {
        var model = new DLCS.HydraModel.Image
        {
            Width = 10, DeliveryChannels = dc.Split(","), MediaType = mediaType
        };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Width)
            .WithErrorMessage("Should not include width");
    }
    
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("video/mp4")]
    [InlineData("application/pdf")]
    public void Width_Allowed_IfFileOnly_AndVideoOrImage(string mediaType)
    {
        var model = new DLCS.HydraModel.Image
        {
            MediaType = mediaType, DeliveryChannels = new[] { "file" }, Width = 10
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.Width);
    }

    [Fact]
    public void Height_Provided()
    {
        var model = new DLCS.HydraModel.Image { Height = 10 };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Height)
            .WithErrorMessage("Should not include height");
    }
    
    [Theory]
    [InlineData("image/jpeg", "file,iiif-img")]
    [InlineData("video/mp4", "file,iiif-av")]
    [InlineData("audio/mp4", "file")]
    public void Height_Provided_NotFileOnly_OrAudio(string mediaType, string dc)
    {
        var model = new DLCS.HydraModel.Image
        {
            Height = 10, DeliveryChannels = dc.Split(","), MediaType = mediaType
        };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Height)
            .WithErrorMessage("Should not include height");
    }
    
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("video/mp4")]
    [InlineData("application/pdf")]
    public void Height_Allowed_IfFileOnly_AndVideoOrImage(string mediaType)
    {
        var model = new DLCS.HydraModel.Image
        {
            MediaType = mediaType, DeliveryChannels = new[] { "file" }, Height = 10
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.Height);
    }
    
    [Fact]
    public void Duration_Provided()
    {
        var model = new DLCS.HydraModel.Image { Duration = 10 };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Duration)
            .WithErrorMessage("Should not include duration");
    }
   
    [Theory]
    [InlineData("image/jpeg", "file")]
    [InlineData("video/mp4", "file,iiif-av")]
    [InlineData("audio/mp4", "file,iiif-av")]
    public void Duration_Provided_NotFileOnly_OrImage(string mediaType, string dc)
    {
        var model = new DLCS.HydraModel.Image
        {
            Duration = 10, DeliveryChannels = dc.Split(","), MediaType = mediaType
        };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Duration)
            .WithErrorMessage("Should not include duration");
    }
    
    [Theory]
    [InlineData("audio/mp4")]
    [InlineData("video/mp4")]
    [InlineData("application/pdf")]
    public void Duration_Allowed_IfFileOnly_AndVideoOrAudio(string mediaType)
    {
        var model = new DLCS.HydraModel.Image
        {
            MediaType = mediaType, DeliveryChannels = new[] { "file" }, Duration = 10
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.Duration);
    }

    [Fact]
    public void Finished_Provided()
    {
        var model = new DLCS.HydraModel.Image { Finished = DateTime.Today };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.Finished);
    }
    
    [Fact]
    public void Created_Provided()
    {
        var model = new DLCS.HydraModel.Image { Created = DateTime.Today };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.Created);
    }

    [Theory]
    [InlineData("file")]
    [InlineData("file,iiif-av")]
    [InlineData("iiif-av")]
    public void UseOriginalPolicy_NotImage(string dc)
    {
        var model = new DLCS.HydraModel.Image
        {
            DeliveryChannels = dc.Split(","),
            MediaType = "image/jpeg",
            ImageOptimisationPolicy = KnownImageOptimisationPolicy.UseOriginalId
        };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.ImageOptimisationPolicy)
            .WithErrorMessage("ImageOptimisationPolicy 'use-original' only valid for image delivery-channel");
    }
    
    [Theory]
    [InlineData("iiif-img")]
    [InlineData("file,iiif-img")]
    public void UseOriginalPolicy_Image(string dc)
    {
        var model = new DLCS.HydraModel.Image
        {
            DeliveryChannels = dc.Split(","),
            MediaType = "image/jpeg",
            ImageOptimisationPolicy = KnownImageOptimisationPolicy.UseOriginalId
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.ImageOptimisationPolicy);
    }
    
    [Fact]
    public void DeliveryChannel_CanBeEmpty()
    {
        var model = new DLCS.HydraModel.Image();
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.DeliveryChannels);
    }
    
    [Theory]
    [InlineData("file")]
    [InlineData("iiif-av")]
    [InlineData("iiif-img")]
    [InlineData("file,iiif-av,iiif-img")]
    public void DeliveryChannel_CanContainKnownValues(string knownValues)
    {
        var model = new DLCS.HydraModel.Image { DeliveryChannels = knownValues.Split(',') };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.DeliveryChannels);
    }
    
    [Fact]
    public void DeliveryChannel_UnknownValue()
    {
        var model = new DLCS.HydraModel.Image { DeliveryChannels = new[] { "foo" } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.DeliveryChannels);
    }
    
        
    [Fact]
    public void DeliveryChannel_ValidationError_WhenDeliveryChannelsDisabled()
    {
        var apiSettings = new ApiSettings();
        var imageValidator = new HydraImageValidator(Options.Create(apiSettings));
        var model = new DLCS.HydraModel.Image { DeliveryChannels = new[] { "iiif-img" } };
        var result = imageValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.DeliveryChannels);
    }
    [Fact]
    public void DeliveryChannel_NoValidationError_WhenDeliveryChannelsDisabled()
    {
        var apiSettings = new ApiSettings();
        var imageValidator = new HydraImageValidator(Options.Create(apiSettings));
        var model = new DLCS.HydraModel.Image();
        var result = imageValidator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.DeliveryChannels);
    }
    
    [Theory]
    [InlineData("some id")]
    [InlineData("some\\id")]
    public void Id_HasValidationErrors_WhenCalledWithRestrictedCharacters(string id)
    {
        var model = new DLCS.HydraModel.Image
        {
            ModelId = id,
            MediaType = "image/jpeg",
            ImageOptimisationPolicy = KnownImageOptimisationPolicy.UseOriginalId
        };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.ModelId);
    }
    
    [Fact]
    public void Id_HasNoValidationErrors_WhenCalledWithoutRestrictedCharacters()
    {
        var model = new DLCS.HydraModel.Image
        {
            ModelId = "someId",
            MediaType = "image/jpeg",
            ImageOptimisationPolicy = KnownImageOptimisationPolicy.UseOriginalId
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.ModelId);
    }
    
    [Fact]
    public void Id_HasNoValidationErrors_WhenCalledWithStrictAssetIdDisabled()
    {
        var model = new DLCS.HydraModel.Image
        {
            ModelId = "some Id",
            MediaType = "image/jpeg",
            ImageOptimisationPolicy = KnownImageOptimisationPolicy.UseOriginalId
        };
        
        var apiSettings = new ApiSettings()
        {
            DeliveryChannelsEnabled = true, 
            RestrictedAssetIdCharacters = "\\ ", 
            DisableStrictAssetIdChecks = true
        };
        var validator = new HydraImageValidator(Options.Create(apiSettings));
        
        var result = validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.ModelId);
    }
    
    [Theory]
    [InlineData("some id")]
    [InlineData("some\\id")]
    [InlineData("someId")]
    public void Id_HasNoValidationErrors_WhenCalledWithEmptyStrictAssetIdCharacters(string id)
    {
        var model = new DLCS.HydraModel.Image
        {
            ModelId = id,
            MediaType = "image/jpeg",
            ImageOptimisationPolicy = KnownImageOptimisationPolicy.UseOriginalId
        };
        
        var apiSettings = new ApiSettings()
        {
            DeliveryChannelsEnabled = true, 
            RestrictedAssetIdCharacters = "", 
            DisableStrictAssetIdChecks = true
        };
        var validator = new HydraImageValidator(Options.Create(apiSettings));
        
        var result = validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.ModelId);
    }
}