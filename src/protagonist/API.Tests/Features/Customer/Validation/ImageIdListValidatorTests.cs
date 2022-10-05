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
                new IdentifierOnly(), new IdentifierOnly(), new IdentifierOnly(), new IdentifierOnly(),
                new IdentifierOnly()
            }
        };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members);
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