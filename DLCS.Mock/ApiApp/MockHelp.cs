using System.Collections.Generic;
using System.Linq;
using DLCS.HydraModel;

namespace DLCS.Mock.ApiApp
{
    public static class MockHelp
    {
        public static Customer GetByName(this List<Customer> customers, string name)
        {
            return customers.Single(c => c.Name == name);
        }
        public static AuthService GetByIdPart(this List<AuthService> authServices, string idPart)
        {
            return authServices.Single(a => a.ModelId == idPart);
        }
        public static Role GetByCustAndId(this List<Role> roles, int customerId, string idPart)
        {
            return roles.Single(r => r.CustomerId == customerId && r.ModelId == idPart);
        }
    }
}