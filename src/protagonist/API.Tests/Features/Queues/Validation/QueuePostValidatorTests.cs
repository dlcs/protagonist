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
    private QueuePostValidator GetSut()
    {
        var apiSettings = new ApiSettings
        {
            MaxBatchSize = 4
        };
        
        return new QueuePostValidator(Options.Create(apiSettings));
    }
 
    [Fact]
    public void Members_Null()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image>();
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members);
    }
    
    [Fact]
    public void Members_Empty()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image> { Members = Array.Empty<Image>() };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members);
    }

    [Fact]
    public void Members_GreaterThanMaxBatchSize()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image>
        {
            Members = new[]
            {
                new Image { ModelId = "f" },
                new Image { ModelId = "fo" },
                new Image { ModelId = "foo" },
                new Image { ModelId = "bar" },
                new Image { ModelId = "baz" },
            }
        };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members)
            .WithErrorMessage("Maximum assets in single batch is 4");
    }
    
    [Fact]
    public void Members_EqualMaxBatchSize_Valid()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image>
        {
            Members = new[]
            {
                new Image { ModelId = "fo" },
                new Image { ModelId = "foo" },
                new Image { ModelId = "bar" },
                new Image { ModelId = "baz" },
            }
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(r => r.Members);
    }

    [Fact]
    public void Members_ContainsDuplicateIds()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image>
        {
            Members = new[]
            {
                new Image { ModelId = "foo", Space = 2 },
                new Image { ModelId = "bar", Space = 2 },
                new Image { ModelId = "foo", Space = 2 },
                new Image { ModelId = "foo", Space = 3 },
            }
        };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(r => r.Members)
            .WithErrorMessage("Members contains 1 duplicate Id(s): Id:foo,Space:2");
    }
    
    [Fact]
    public void Members_NoValidationError_IfContainsDuplicateIds_WithDifferentSpace()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image>
        {
            Members = new[]
            {
                new Image { ModelId = "foo", Space = 10, },
                new Image { ModelId = "bar", Space = 10, },
                new Image { ModelId = "foo", Space = 20, },
            }
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(r => r.Members);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Member_ModelId_NullOrEmpty(string id)
    {
        var sut = GetSut();
        var model = new HydraCollection<Image> { Members = new[] { new Image { ModelId = id } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].ModelId");
    }
    
    [Fact]
    public void Member_Space_Default()
    {
        var sut = GetSut();
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
        var sut = GetSut();
        var model = new HydraCollection<Image> { Members = new[] { new Image { MediaType = mediaType } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].MediaType");
    }

    [Fact]
    public void Member_Batch_Provided()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image> { Members = new[] { new Image { Batch = "10" } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].Batch");
    }
    
    [Fact]
    public void Member_Width_Provided()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image> { Members = new[] { new Image { Width = 10 } } };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor("Members[0].Width")
            .WithErrorMessage("Should not include width");
    }

    [Fact]
    public void Member_Height_Provided()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image> { Members = new[] { new Image { Height = 10 } } };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor("Members[0].Height")
            .WithErrorMessage("Should not include height");
    }
    
    [Fact]
    public void Member_Duration_Provided()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image> { Members = new[] { new Image { Duration = 10 } } };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor("Members[0].Duration")
            .WithErrorMessage("Should not include duration");
    }
    
    [Fact]
    public void Member_Finished_Provided()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image> { Members = new[] { new Image { Finished = DateTime.Today } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].Finished");
    }
    
    [Fact]
    public void Member_Created_Provided()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image> { Members = new[] { new Image { Created = DateTime.Today } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].Created");
    }
    
    [Fact]
    public void Member_ImageOptimisationPolicy_Provided()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image> { Members = new[] { new Image { ImageOptimisationPolicy = "my-policy" } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].ImageOptimisationPolicy");
    }

    [Fact]
    public void Member_ThumbnailPolicy_Provided()
    {
        var sut = GetSut();
        var model = new HydraCollection<Image> { Members = new[] { new Image { ThumbnailPolicy = "my-policy" } } };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].ThumbnailPolicy");
    }
}
