using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLCS.Model.Customer
{
    public interface ICustomerRepository
    {
        /// <summary>
        /// Get lookup of displayName:id for all customers
        /// </summary>
        /// <returns></returns>
        public Task<Dictionary<string, int>> GetCustomerIdLookup();
    }
}
