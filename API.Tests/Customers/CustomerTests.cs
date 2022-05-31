using API.Features.Customer.Requests;
using DLCS.HydraModel;
using FluentAssertions;
using Xunit;

namespace API.Tests.Customers;

public class CustomerTests
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

    [Fact]
    public void CreateNewCustomer_Throws_IfNameConflicts()
    {
        // Tests CreateCustomer::EnsureCustomerNamesNotTaken
    }



    [Fact]
    public void NewCustomerId_Increments_EntityCounter()
    {
        // Test... The Controller?
        // Or the Mediatr command?
        // Or the Repository operations?
        
        // Expected: Adding a new Customer picks a Customer ID that
        // - is not in use
        // - is higher than any other customer
        
        // Tests CreateCustomer::GetIdForNewCustomer
    }

    [Fact]
    public void CreateNewCustomer_Creates_SpaceEntityCounter()
    {
        // An entity counter for this customer's spaces is created with 
        // Type=space, Scope=CustomerId, Next=0 (?), Customer=CustomerId
    }
    
    [Fact]
    public void CreateNewCustomer_Creates_CustomerQueue()
    {
        // A Queue for this customer is created with 
        // Customer=CustomerId, Name=default, Size=0
    }
    
    [Fact]
    public void CreateNewCustomer_Creates_ClickthroughAuthService()
    {
        // Creates an Auth service with
        // GUID Id, Customer=customerId, Name="clickthrough", ttl=600
        // (deliverator doesn't set profile?)
    }
    
    
    [Fact]
    public void CreateNewCustomer_Creates_LogoutAuthService()
    {
        // Creates an Auth service with
        // GUID Id, Customer=customerId, Name="logout", ttl=600, profile=http://iiif.io/api/auth/0/logout"
    }
    
    
    [Fact]
    public void LogoutAuthService_IsChildOf_Clickthrough()
    {
        // The logout service is a child service of the clickthrough service
        // test that this is reflected in the returned Hydra Customer? As a URL?
    }
    
    
    [Fact]
    public void NewCustomer_Has_ClickthroughRole()
    {
        // Creates a role with
        // Id URL having correct customer Id, Customer=CustomerId, AuthService=^^, Name=clickthrough
        
    }
    
}