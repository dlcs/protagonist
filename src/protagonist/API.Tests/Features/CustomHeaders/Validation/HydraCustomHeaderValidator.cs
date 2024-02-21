using System;
using API.Features.CustomHeaders.Validation;
using DLCS.HydraModel;
using FluentValidation.TestHelper;

namespace API.Tests.Features.CustomHeaders.Validation;

public class HydraCustomHeaderValidatorTests
{
    private readonly HydraCustomHeaderValidator sut;
    
    public HydraCustomHeaderValidatorTests()
    {
        sut = new HydraCustomHeaderValidator();
    }
    
    [Fact]
    public void NewCustomHeader_CannotHave_AssetId()
    {
        var customHeader = new CustomHeader()
        {
            Id = Guid.NewGuid().ToString()
        };
        var result = sut.TestValidate(customHeader);
        result.ShouldHaveValidationErrorFor(ch => ch.Id);
    }
    
    [Fact]
    public void NewCustomHeader_CannotHave_CustomerId()
    {
        var customHeader = new CustomHeader()
        {
            CustomerId = 1
        };
        var result = sut.TestValidate(customHeader);
        result.ShouldHaveValidationErrorFor(ch => ch.CustomerId);
    }
    
    [Fact]
    public void NewCustomHeader_Requires_Key()
    {
        var customHeader = new CustomHeader()
        {
            Key = null
        };
        var result = sut.TestValidate(customHeader);
        result.ShouldHaveValidationErrorFor(ch => ch.Key);
    }
    
    [Fact]
    public void NewCustomHeader_Requires_Value()
    {
        var customHeader = new CustomHeader()
        {
            Value = null
        };
        var result = sut.TestValidate(customHeader);
        result.ShouldHaveValidationErrorFor(ch => ch.Value);
    }
}