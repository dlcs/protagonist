using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLCS.Model.Customers
{
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
    }
}
