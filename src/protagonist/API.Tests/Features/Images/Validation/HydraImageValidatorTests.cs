using System;
using API.Features.Image.Validation;
using DLCS.Model.Policies;
using FluentValidation.TestHelper;
using AssetFamily = DLCS.HydraModel.AssetFamily;

namespace API.Tests.Features.Images.Validation;

public class HydraImageValidatorTests
{
    private readonly HydraImageValidator sut;

    public HydraImageValidatorTests()
    {
        sut = new HydraImageValidator();
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
    public void Family_NotSet()
    {
        var model = new DLCS.HydraModel.Image { Family = null };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(a => a.Family);
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
    
    [Fact]
    public void Width_NotSet_NoTranscodePolicy()
    {
        var model = new DLCS.HydraModel.Image { ImageOptimisationPolicy = "none" };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Width)
            .WithErrorMessage("Width cannot be empty if 'none' imageOptimisationPolicy specified");
    }
    
    [Theory]
    [InlineData("audio/mp4", AssetFamily.Timebased)]
    [InlineData("audio/mp4", AssetFamily.File)]
    [InlineData("application/pdf", AssetFamily.File)]
    public void WidthHeight_NotSet_NoTranscodePolicy_AllowedForFileOrAudio(string mediaType, AssetFamily assetFamily)
    {
        var model = new DLCS.HydraModel.Image
        {
            ImageOptimisationPolicy = "none", Family = assetFamily, MediaType = mediaType
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.Width);
        result.ShouldNotHaveValidationErrorFor(a => a.Height);
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
    
    [Fact]
    public void Height_NotSet_NoTranscodePolicy()
    {
        var model = new DLCS.HydraModel.Image { ImageOptimisationPolicy = "none" };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Height)
            .WithErrorMessage("Height cannot be empty if 'none' imageOptimisationPolicy specified");
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
    
    [Fact]
    public void Duration_Provided_NoTranscode_ButImage()
    {
        var model = new DLCS.HydraModel.Image
            { Duration = 10, ImageOptimisationPolicy = "none", Family = AssetFamily.Image };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Duration)
            .WithErrorMessage("Should not include duration");
    }
    
    [Fact]
    public void Duration_NotSetForTimebased_NoTranscodePolicy()
    {
        var model = new DLCS.HydraModel.Image { ImageOptimisationPolicy = "none", Family = AssetFamily.Timebased };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Duration)
            .WithErrorMessage(
                "Duration cannot be empty if 'none' imageOptimisationPolicy specified for timebased asset");
    }
    
    [Theory]
    [InlineData(AssetFamily.File)]
    [InlineData(AssetFamily.Image)]
    public void Duration_NotSet_NoTranscodePolicy_AllowedForFileOrImageFamily(AssetFamily family)
    {
        var model = new DLCS.HydraModel.Image { ImageOptimisationPolicy = "none", Family = family };
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
    
    [Fact]
    public void FamilyT_NotAudioOrVideoMediaType()
    {
        var model = new DLCS.HydraModel.Image { Family = AssetFamily.Timebased, MediaType = "application/pdf" };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.MediaType)
            .WithErrorMessage("Timebased assets must have mediaType starting video/ or audio/");
    }
    
    [Theory]
    [InlineData(AssetFamily.Timebased)]
    [InlineData(AssetFamily.File)]
    public void UseOriginalPolicy_NotImage(AssetFamily family)
    {
        var model = new DLCS.HydraModel.Image
            { Family = family, ImageOptimisationPolicy = KnownImageOptimisationPolicy.UseOriginalId };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(a => a.Family)
            .WithErrorMessage("ImageOptimisationPolicy 'use-original' only valid for Image family");
    }
    
    [Fact]
    public void UseOriginalPolicy_Image()
    {
        var model = new DLCS.HydraModel.Image
            { Family = AssetFamily.Image, ImageOptimisationPolicy = KnownImageOptimisationPolicy.UseOriginalId };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(a => a.Family);
    }
}