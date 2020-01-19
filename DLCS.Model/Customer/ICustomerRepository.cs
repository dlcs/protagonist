using System.Collections.Generic;

namespace DLCS.Model.Customer
{
    public interface ICustomerRepository
    {
        public Dictionary<string, int> GetCustomerIdLookup();
    }
}
