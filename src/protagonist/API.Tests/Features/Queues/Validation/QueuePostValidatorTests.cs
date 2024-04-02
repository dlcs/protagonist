using System;
using API.Features.Queues.Validation;
using API.Settings;
using DLCS.HydraModel;
using FluentValidation.TestHelper;
using Hydra.Collections;
using Microsoft.Extensions.Options;

namespace API.Tests.Features.Queues.Validation;

public class QueuePostValidatorTests
{
    private readonly QueuePostValidator sut;
    private readonly QueuePostValidator sutWithOldDcEmulation;
    
    public QueuePostValidatorTests()
    {
        var apiSettingsA = new ApiSettings { MaxBatchSize = 4 };
        sut = new QueuePostValidator(Options.Create(apiSettingsA));
        
        var apiSettingsB = new ApiSettings { EmulateOldDeliveryChannelProperties = true};
        sutWithOldDcEmulation = new QueuePostValidator(Options.Create(apiSettingsB));
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
    public void Member_Space_Default()
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { Space = 0 } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].Space");
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Member_MediaType_NullOrEmpty(string mediaType)
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { MediaType = mediaType } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].MediaType");
    }

    [Fact]
    public void Member_Batch_Provided()
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { Batch = "10" } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].Batch");
    }
    
    [Fact]
    public void Member_Width_Provided()
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { Width = 10 } } };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor("Members[0].Width")
            .WithErrorMessage("Should not include width");
    }

    [Fact]
    public void Member_Height_Provided()
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { Height = 10 } } };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor("Members[0].Height")
            .WithErrorMessage("Should not include height");
    }
    
    [Fact]
    public void Member_Duration_Provided()
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { Duration = 10 } } };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor("Members[0].Duration")
            .WithErrorMessage("Should not include duration");
    }
    
    [Fact]
    public void Member_Finished_Provided()
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { Finished = DateTime.Today } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].Finished");
    }
    
    [Fact]
    public void Member_Created_Provided()
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { Created = DateTime.Today } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].Created");
    }
    
    [Fact]
    public void Member_ImageOptimisationPolicy_Null_WhenOldDeliveryChannelEmulationDisabled()
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { ImageOptimisationPolicy = "my-policy" } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].ImageOptimisationPolicy");
    }
    
    [Fact]
    public void Member_ThumbnailPolicy_Null_WhenOldDeliveryChannelEmulationDisabled()
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { ThumbnailPolicy = "my-policy" } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].ThumbnailPolicy");
    }
    
    [Fact]
    public void Member_ImageOptimisationPolicy_Allowed_WhenOldDeliveryChannelEmulationEnabled()
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { ImageOptimisationPolicy = "my-policy" } } };
        var result = sutWithOldDcEmulation.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor("Members[0].ImageOptimisationPolicy");
    }
    
    [Fact]
    public void Member_ThumbnailPolicy_Allowed_WhenOldDeliveryChannelEmulationEnabled()
    {
        var model = new HydraCollection<Image> { Members = new[] { new Image { ThumbnailPolicy = "my-policy" } } };
        var result = sutWithOldDcEmulation.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor("Members[0].ThumbnailPolicy");
    }
}