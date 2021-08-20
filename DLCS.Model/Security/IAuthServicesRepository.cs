using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLCS.Model.Security
{
    public interface IAuthServicesRepository
    {
        /// <summary>
        /// Get list of all AuthServices (Parent + Child) for customer and role
        /// </summary>
        /// <param name="customer">Id of customer</param>
        /// <param name="role">Full role identifier (e.g. https://api.dlcs.digirati.io/customers/2/roles/clickthrough)</param>
        /// <returns>List of authServices</returns>
        public Task<IEnumerable<AuthService>> GetAuthServicesForRole(int customer, string role);
    }
}