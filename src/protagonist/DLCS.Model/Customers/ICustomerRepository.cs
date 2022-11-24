using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLCS.Model.Customers;

public interface ICustomerRepository
{
    /// <summary>
    /// Get lookup of displayName:id for all customers
    /// </summary>
    /// <returns></returns>
    public Task<Dictionary<string, int>> GetCustomerIdLookup();

    /// <summary>
    /// Get Customer with specified Id
    /// </summary>
    /// <param name="customerId">Id of customer to get</param>
    /// <returns><see cref="Customer"/> object if found, else null</returns>
    public Task<Customer?> GetCustomer(int customerId);

    /// <summary>
    /// Get Customer with specified Name
    /// </summary>
    /// <param name="name">The name (url-part) of the customer (not display name)</param>
    /// <returns><see cref="Customer"/> object if found, else null</returns>
    public Task<Customer?> GetCustomer(string name);
    
    /// <summary>
    /// Return the customer that owns the supplied apiKey.
    /// This might be the admin user or it might be a regular customer.
    /// This method will always return the customer that owns the key, but that key owner
    /// might be an admin user rather than the customer indicated by customerIdHint.
    /// </summary>
    /// <param name="apiKey">The required api key</param>
    /// <param name="customerIdHint">
    /// If supplied, a customer will only be returned if it either matches
    /// this customerId, or is an admin key.
    /// If not supplied, this method can only return the admin user if it returns anything.
    /// </param>
    /// <returns></returns>
    public Task<Customer?> GetCustomerForKey(string apiKey, int? customerIdHint);
}
