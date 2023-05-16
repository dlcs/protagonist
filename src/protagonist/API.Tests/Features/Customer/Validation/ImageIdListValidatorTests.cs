using System;
using API.Features.Customer.Validation;
using API.Settings;
using DLCS.Model;
using FluentValidation.TestHelper;
using Hydra.Collections;
using Microsoft.Extensions.Options;

namespace API.Tests.Features.Customer.Validation;

public class ImageIdListValidatorTests
{
    private readonly ImageIdListValidator sut;

    public ImageIdListValidatorTests()
    {
        var apiSettings = new ApiSettings { MaxImageListSize = 4 };
        sut = new ImageIdListValidator(Options.Create(apiSettings));
    }

    [Fact]
    public void Members_Null()
    {
        var model = new HydraCollection<IdentifierOnly>();
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members);
    }
    
    [Fact]
    public void Members_Empty()
    {
        var model = new HydraCollection<IdentifierOnly> { Members = Array.Empty<IdentifierOnly>() };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members);
    }

    [Fact]
    public void Members_GreaterThanMaxBatchSize()
    {
        var model = new HydraCollection<IdentifierOnly>
        {
            Members = new[]
            {
                new IdentifierOnly { Id = "one" }, new IdentifierOnly { Id = "two" },
                new IdentifierOnly { Id = "three" }, new IdentifierOnly { Id = "four" },
                new IdentifierOnly { Id = "five" }
            }
        };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(r => r.Members)
            .WithErrorMessage("Maximum assets in single batch is 4");
    }
    
    [Fact]
    public void Members_EqualToMaxBatchSize_IsValid()
    {
        var model = new HydraCollection<IdentifierOnly>
        {
            Members = new[]
            {
                new IdentifierOnly { Id = "one" }, new IdentifierOnly { Id = "two" },
                new IdentifierOnly { Id = "three" }, new IdentifierOnly { Id = "four" },
            }
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(r => r.Members);
    }

    [Fact]
    public void Members_ContainsDuplicateIds()
    {
        var model = new HydraCollection<IdentifierOnly>
        {
            Members = new[]
            {
                new IdentifierOnly { Id = "1/2/foo" },
                new IdentifierOnly { Id = "1/2/bar" },
                new IdentifierOnly { Id = "1/2/foo" },
            }
        };
        var result = sut.TestValidate(model);
        result
            .ShouldHaveValidationErrorFor(r => r.Members)
            .WithErrorMessage("Members contains 1 duplicate Id(s): 1/2/foo");
    }
}