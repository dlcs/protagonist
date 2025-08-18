using System.Threading.Tasks;
using DLCS.Model.PathElements;

namespace DLCS.Repository.CustomerPath;

/// <summary>
/// Base template for <see cref="IPathCustomerRepository"/> implementations
/// </summary>
public abstract class CustomerPathElementTemplate : IPathCustomerRepository
{
    public async Task<CustomerPathElement> GetCustomerPathElement(string customerPart)
    {
        // customerPart can be an int or a string name
        if (int.TryParse(customerPart, out var customerId))
        {
            var customerName = await GetCustomerName(customerId);
            return new CustomerPathElement(customerId, customerName);
        }
        else
        {
            customerId = await GetCustomerId(customerPart);
            return new CustomerPathElement(customerId, customerPart);
        }
    }

    protected abstract Task<int> GetCustomerId(string customerName);
    
    protected abstract Task<string> GetCustomerName(int customerId);
}