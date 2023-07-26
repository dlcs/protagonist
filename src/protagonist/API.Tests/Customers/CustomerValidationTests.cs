using API.Features.Customer.Validation;
using DLCS.HydraModel;
using FluentValidation.TestHelper;

namespace API.Tests.Customers;

public class CustomerValidationTests
{
    private readonly HydraCustomerValidator sut;
    
    public CustomerValidationTests()
    {
        sut = new HydraCustomerValidator();
    }

    [Fact]
    public void NewCustomer_RequiresOnly_Names()
    {
        var customer = new Customer
        {
            Name = "my-test-customer",
            DisplayName = "My test customer"
        };
        
        var result = sut.TestValidate(customer);

        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void NewCustomer_Requires_Fields()
    {
        var customer = new Customer();
        
        var result = sut.TestValidate(customer);

        result.ShouldHaveValidationErrorFor(c => c.Name);
        result.ShouldHaveValidationErrorFor(c => c.DisplayName);
    }
    
    [Fact]
    public void NewCustomer_CannotBe_Admin()
    {
        var customer = new Customer
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
    public void NewCustomer_CannotHaveName_Admin(string invalidName)
    {
        var customer = new Customer
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
    public void NewCustomer_CannotHaveName_Version(string invalidName)
    {
        var customer = new Customer
        {
            Name = invalidName,
            DisplayName = "My test customer"
        };
        
        var result = sut.TestValidate(customer);

        result
            .ShouldHaveValidationErrorFor(c => c.Name)
            .WithErrorMessage($"Name field [{invalidName}] cannot start with a version slug.");
    }
}