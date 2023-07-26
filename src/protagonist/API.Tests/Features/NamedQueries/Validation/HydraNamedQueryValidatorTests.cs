using System;
using API.Features.NamedQueries.Converters;
using DLCS.HydraModel;
using FluentValidation.TestHelper;

namespace API.Tests.Features.NamedQueries.Validation;

public class HydraNamedQueryValidatorTests
{
    private readonly HydraNamedQueryValidator sut;
    
    public HydraNamedQueryValidatorTests()
    {
        sut = new HydraNamedQueryValidator();
    }
    
    [Fact]
    public void NewNamedQuery_CannotHave_AssetId()
    {
        var namedQuery = new NamedQuery()
        {
            Id = Guid.NewGuid().ToString()
        };
        var result = sut.TestValidate(namedQuery);
        result.ShouldHaveValidationErrorFor(nq => nq.Id);
    }
    
    [Fact]
    public void NewNamedQuery_CannotHave_CustomerId()
    {
        var namedQuery = new NamedQuery()
        {
            CustomerId = 1
        };
        var result = sut.TestValidate(namedQuery);
        result.ShouldHaveValidationErrorFor(nq => nq.CustomerId);
    }
    
    [Fact]
    public void NewNamedQuery_Requires_Name()
    {
        var namedQuery = new NamedQuery();
        var result = sut.TestValidate(namedQuery);
        result.ShouldHaveValidationErrorFor(nq => nq.Name);
    }
    
    [Fact]
    public void NewNamedQuery_Requires_Template()
    {
        var namedQuery = new NamedQuery();
        var result = sut.TestValidate(namedQuery);
        result.ShouldHaveValidationErrorFor(nq => nq.Template);
    }
}
