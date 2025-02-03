using API.Features.Customer.Validation;
using FluentValidation.TestHelper;
using CustomerModel = DLCS.HydraModel.Customer;

namespace API.Tests.Features.Customers;

public class HydraCustomerValidatorTests
{
    private readonly HydraCustomerValidator sut = new();

    [Fact]
    public void RequiresOnly_Names()
    {
        var customer = new CustomerModel
        {
            Name = "my-test-customer",
            DisplayName = "My test customer"
        };
        
        var result = sut.TestValidate(customer);

        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Requires_Fields()
    {
        var customer = new CustomerModel();
        
        var result = sut.TestValidate(customer);

        result.ShouldHaveValidationErrorFor(c => c.Name);
        result.ShouldHaveValidationErrorFor(c => c.DisplayName);
    }
    
    [Fact]
    public void CannotBe_Admin()
    {
        var customer = new CustomerModel
        {
            Name = "my-test-customer",
            DisplayName = "My test customer",
            Administrator = true,
        };

        var result = sut.TestValidate(customer);

        result.ShouldHaveValidationErrorFor(c => c.Administrator);
    }
    
    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    public void CannotHaveName_Admin(string invalidName)
    {
        var customer = new CustomerModel
        {
            Name = invalidName,
            DisplayName = "My test customer"
        };

        var result = sut.TestValidate(customer);

        result
            .ShouldHaveValidationErrorFor(c => c.Name)
            .WithErrorMessage($"Name field [{invalidName}] cannot be a reserved word.");
    }
    
    [Theory]
    [InlineData("v2-customer")]
    [InlineData("v2")]
    [InlineData("v3")]
    public void CannotHaveName_StartingWithVersion(string invalidName)
    {
        var customer = new CustomerModel
        {
            Name = invalidName,
            DisplayName = "My test customer"
        };
        
        var result = sut.TestValidate(customer);

        result
            .ShouldHaveValidationErrorFor(c => c.Name)
            .WithErrorMessage($"Name field [{invalidName}] cannot start with a version slug.");
    }
    
    [Theory]
    [InlineData("customer-v2")]
    [InlineData("vv2")]
    [InlineData("custv3omer")]
    public void CanHaveName_WithVersionBeyondStart(string validName)
    {
        var customer = new CustomerModel
        {
            Name = validName,
            DisplayName = "My test customer"
        };
        
        var result = sut.TestValidate(customer);

        result
            .ShouldNotHaveValidationErrorFor(c => c.Name);
    }
    
    [Theory]
    [InlineData("customer|")]
    [InlineData("cust%omer")]
    [InlineData("cus,tomer")]
    public void CannotHaveName_WithInvalidCharacters(string invalidName)
    {
        var customer = new CustomerModel
        {
            Name = invalidName,
            DisplayName = "My test customer"
        };
        
        var result = sut.TestValidate(customer);

        result
            .ShouldHaveValidationErrorFor(c => c.Name)
            .WithErrorMessage($"Name field [{invalidName}] contains invalid characters. Accepted: [a-z] [A-Z] [0-9] - _ and .");
    }
    
    [Theory]
    [InlineData("customer-")]
    [InlineData("Customer192")]
    [InlineData("21the-customer_here.now")]
    public void CanHaveName_WithValidInvalidCharacters(string invalidName)
    {
        var customer = new CustomerModel
        {
            Name = invalidName,
            DisplayName = "My test customer"
        };
        
        var result = sut.TestValidate(customer);

        result.ShouldNotHaveValidationErrorFor(c => c.Name);
    }
    
    [Fact]
    public void CannotHaveName_WithMoreThan60Characters()
    {
        var customer = new CustomerModel
        {
            Name = new string('a', 61),
            DisplayName = "My test customer"
        };
        
        var result = sut.TestValidate(customer);

        result
            .ShouldHaveValidationErrorFor(c => c.Name)
            .WithErrorMessage("The length of 'Name' must be 60 characters or fewer. You entered 61 characters.");
    }
}