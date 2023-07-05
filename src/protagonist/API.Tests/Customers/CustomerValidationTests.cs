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
    
    private Customer GetValidNewCustomer()
    {
        return new Customer
        {
            Name = "my-test-customer",
            DisplayName = "My test customer"
        };
    }
    
    [Fact]
    public void NewCustomer_RequiresOnly_Names()
    {
        var customer = GetValidNewCustomer();
        
        var result = sut.TestValidate(customer);

        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void NewCustomer_Requires_Fields()
    {
        var customer = new Customer();
        
        var result = sut.TestValidate(customer);

        result.ShouldHaveAnyValidationError();
    }
    
    [Fact]
    public void NewCustomer_CannotBe_Admin()
    {
        var customer = GetValidNewCustomer();
        customer.Administrator = true;
        
        var result = sut.TestValidate(customer);

        result.ShouldHaveAnyValidationError();
    }
    
    [Fact]
    public void NewCustomer_CannotHaveName_Admin()
    {
        var customer = GetValidNewCustomer();
        customer.Name = "Admin";
        
        var result = sut.TestValidate(customer);

        result.ShouldHaveAnyValidationError();
    }
    
    [Fact]
    public void NewCustomer_CannotHaveName_Version()
    {
        var customer = GetValidNewCustomer();
        customer.Name = "v2-customer";
        
        var result = sut.TestValidate(customer);

        result.ShouldHaveAnyValidationError();
    }
}