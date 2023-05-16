using API.Features.Customer.Requests;
using API.Features.Customer.Validation;
using DLCS.HydraModel;
using FluentAssertions;
using Xunit;

namespace API.Tests.Customers;

public class CustomerValidationTests
{
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
        
        var errors = HydraCustomerValidator.GetNewHydraCustomerErrors(customer);

        errors.Length.Should().Be(0);
    }
    
    [Fact]
    public void NewCustomer_Requires_Fields()
    {
        var customer = new Customer();
        
        var errors = HydraCustomerValidator.GetNewHydraCustomerErrors(customer);

        errors.Length.Should().BeGreaterThan(0);
    }
    
    
    [Fact]
    public void NewCustomer_CannotBe_Admin()
    {
        var customer = GetValidNewCustomer();
        customer.Administrator = true;
        
        var errors = HydraCustomerValidator.GetNewHydraCustomerErrors(customer);

        errors.Length.Should().BeGreaterThan(0);
    }
   
    
    [Fact]
    public void NewCustomer_CannotHaveName_Admin()
    {
        var customer = GetValidNewCustomer();
        customer.Name = "Admin";
        
        var errors = HydraCustomerValidator.GetNewHydraCustomerErrors(customer);

        errors.Length.Should().BeGreaterThan(0);
    }
}