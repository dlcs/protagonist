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
    public void Members_Empty()
    {
        IdentifierOnly[] members = [];
        var result = sut.TestValidate(members);
        result.ShouldHaveValidationErrorFor(r => r)
            .WithErrorMessage("Members cannot be empty");;
    }

    [Fact]
    public void Members_GreaterThanMaxBatchSize()
    {

        var members = new[]
        {
            new IdentifierOnly { Id = "one" }, new IdentifierOnly { Id = "two" },
            new IdentifierOnly { Id = "three" }, new IdentifierOnly { Id = "four" },
            new IdentifierOnly { Id = "five" }
        };
        var result = sut.TestValidate(members);
        result
            .ShouldHaveValidationErrorFor(r => r)
            .WithErrorMessage("Maximum assets in single batch is 4");
    }
    
    [Fact]
    public void Members_EqualToMaxBatchSize_IsValid()
    {
        var members = new[]
        {
            new IdentifierOnly { Id = "one" }, new IdentifierOnly { Id = "two" },
            new IdentifierOnly { Id = "three" }, new IdentifierOnly { Id = "four" },
        };
        var result = sut.TestValidate(members);
        result.ShouldNotHaveValidationErrorFor(r => r);
    }

    [Fact]
    public void Members_ContainsDuplicateIds()
    {
        var members = new[]
        {
            new IdentifierOnly { Id = "1/2/foo" },
            new IdentifierOnly { Id = "1/2/bar" },
            new IdentifierOnly { Id = "1/2/foo" },
        };
        var result = sut.TestValidate(members);
        result
            .ShouldHaveValidationErrorFor(r => r)
            .WithErrorMessage("Members contains 1 duplicate Id(s): 1/2/foo");
    }
}
