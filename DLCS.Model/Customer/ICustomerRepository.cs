using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLCS.Model.Customer
{
    public interface ICustomerRepository
    {
        public Task<Dictionary<string, int>> GetCustomerIdLookup();
    }
}
