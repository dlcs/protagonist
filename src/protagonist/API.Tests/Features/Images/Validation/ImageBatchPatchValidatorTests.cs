using System;
using API.Features.Image.Validation;
using API.Settings;
using DLCS.HydraModel;
using FluentValidation.TestHelper;
using Hydra.Collections;
using Microsoft.Extensions.Options;

namespace API.Tests.Features.Images.Validation;

public class ImageBatchPatchValidatorTests
{
    private readonly ImageBatchPatchValidator sut;

    public ImageBatchPatchValidatorTests()
    {
        var apiSettings = new ApiSettings { MaxBatchSize = 4 };
        sut = new ImageBatchPatchValidator(Options.Create(apiSettings));
    }
    
    [Fact]
    public void Members_Null()
    {
        var model = new HydraCollection<Image>();
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members);
    }
    
    [Fact]
    public void Members_Empty()
    {
        var model = new HydraCollection<Image> { Members = Array.Empty<Image>() };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members);
    }

    [Fact]
    public void Members_GreaterThanMaxBatchSize()
    {
        var model = new HydraCollection<Image>
        {
            Members = new[]
            {
                new Image { ModelId = "1/2/f" },
                new Image { ModelId = "1/2/fo" },
                new Image { ModelId = "1/2/foo" },
                new Image { ModelId = "1/2/bar" },
                new Image { ModelId = "1/2/baz" },
            }
        };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members)
            .WithErrorMessage("Maximum assets in single batch is 4");
    }
    
    [Fact]
    public void Members_EqualMaxBatchSize_Valid()
    {
        var model = new HydraCollection<Image>
        {
            Members = new[]
            {
                new Image { ModelId = "1/2/fo" },
                new Image { ModelId = "1/2/foo" },
                new Image { ModelId = "1/2/bar" },
                new Image { ModelId = "1/2/baz" },
            }
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(r => r.Members);
    }

    [Fact]
    public void Members_ContainsDuplicateIds()
    {
        var model = new HydraCollection<Image>
        {
            Members = new[]
            {
                new Image { ModelId = "1/2/foo" },
                new Image { ModelId = "1/2/bar" },
                new Image { ModelId = "1/2/foo" },
            }
        };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(r => r.Members)
            .WithErrorMessage("Members contains 1 duplicate Id(s): 1/2/foo");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Member_ModelId_NullOrEmpty(string id)
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { ModelId = id } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].ModelId");
    }
    
    [Fact]
    public void Member_Origin_Provided()
    {
        var model = new HydraCollection<Image> { Members = new[]
        {
            new Image { Origin = "https://example.com/images/example-image.jpg" }
        } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].Origin");
    }
    
    [Fact]
    public void Member_ImageOptimisationPolicy_Provided()
    {
        var model = new HydraCollection<Image> { Members = new[]
        {
            new Image { ImageOptimisationPolicy = "example-policy" }
        } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].ImageOptimisationPolicy");
    }
    
    [Fact]
    public void Member_MaxUnauthorised_Provided()
    {
        var model = new HydraCollection<Image> { Members = new[]
        {
            new Image { MaxUnauthorised = 200 }
        } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].MaxUnauthorised");
    }
    
    [Fact]
    public void Member_DeliveryChannels_Provided()
    {
        var model = new HydraCollection<Image> { Members = new[]
        {
            new Image { WcDeliveryChannels = new []{"iiif-img","thumbs"}}
        } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].WcDeliveryChannels");
    }
    
    [Fact]
    public void Member_ThumbnailPolicy_Provided()
    {
        var model = new HydraCollection<Image> { Members = new[]
        {
            new Image { ThumbnailPolicy = "example-policy" }
        } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].ThumbnailPolicy");
    }
}