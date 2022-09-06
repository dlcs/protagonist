using System;
using API.Features.Customer.Validation;
using FluentValidation.TestHelper;

namespace API.Tests.Features.Customer.Validation;

public class CustomerPatchValidatorTests
{
    private readonly CustomerPatchValidator sut;

    public CustomerPatchValidatorTests()
    {
        sut = new CustomerPatchValidator();
    }

    [Fact]
    public void Error_Id_Provided()
    {
        var model = new DLCS.HydraModel.Customer { Id = "foo" };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(c => c.Id);
    }
    
    [Fact]
    public void Error_Name_Provided()
    {
        var model = new DLCS.HydraModel.Customer { Name = "foo" };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Error_Admin_Provided(bool admin)
    {
        var model = new DLCS.HydraModel.Customer { Administrator = admin };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(c => c.Administrator);
    }
    
    [Fact]
    public void Error_Created_Provided()
    {
        var model = new DLCS.HydraModel.Customer { Created = DateTime.Now };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(c => c.Created);
    }

    [Fact]
    public void Error_Keys_Provided()
    {
        var model = new DLCS.HydraModel.Customer { Keys = "foo" };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(c => c.Keys);
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Error_AcceptedAgreement_Provided(bool acceptedAgreement)
    {
        var model = new DLCS.HydraModel.Customer { AcceptedAgreement = acceptedAgreement };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(c => c.AcceptedAgreement);
    }
}