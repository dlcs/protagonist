using System.Collections.Generic;
using DLCS.Model.PathElements;

namespace API.Infrastructure;

public static class PathHelper
{
    private static Dictionary<int, CustomerPathElement> customerPathElements = new();
    
    public static async Task<CustomerPathElement> GetCustomerPathElement(
        int customer, 
        IPathCustomerRepository customerPathRepository)
    {
        if (customerPathElements.TryGetValue(customer, out var prefetchedCustomer)) return prefetchedCustomer;
        
        var customerPathElement = await customerPathRepository.GetCustomerPathElement(customer.ToString());
        customerPathElements[customer] = customerPathElement;
        return customerPathElement;
    }
}
