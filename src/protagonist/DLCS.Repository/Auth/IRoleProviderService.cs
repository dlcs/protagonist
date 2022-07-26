using System.Threading.Tasks;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;

namespace DLCS.Repository.Auth
{
    /// <summary>
    /// Functions for dealing with <see cref="RoleProvider"/> resources
    /// </summary>
    public interface IRoleProviderService
    {
        /// <summary>
        /// Get RoleProviderConfiguration element for specified auth service
        /// </summary>
        /// <param name="customerId">Customer Id for RoleProvider</param>
        /// <param name="authServiceName">AuthService name to fetch RoleProvider configuration for</param>
        /// <returns>RoleProviderConfiguration element if found, else null</returns>
        Task<RoleProviderConfiguration?> GetRoleProviderConfiguration(int customerId, string authServiceName);

        /// <summary>
        /// Get RoleProvider details for specified authService
        /// </summary>
        /// <param name="customerId">Customer Id for RoleProvider</param>
        /// <param name="authServiceName">AuthService name to fetch RoleProvider for</param>
        /// <returns>RoleProvider if found, else null</returns>
        Task<RoleProvider?> GetRoleProviderForAuthService(int customerId, string authServiceName);

        /// <summary>
        /// Get RoleProviderConfiguration details for specified RoleProvider
        /// </summary>
        /// <param name="roleProvider">RoleProvider to get configuration from</param>
        /// <returns>RoleProviderConfiguration if found, else null</returns>
        RoleProviderConfiguration? GetRoleProviderConfiguration(RoleProvider roleProvider);

        /// <summary>
        /// Get credentials from role provider
        /// </summary>
        BasicCredentials? GetCredentialsForRoleProvider(RoleProvider roleProvider);
    }
}