using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Auth.Entities;

namespace DLCS.Model.Auth;

public interface IAuthServicesRepository
{
    /// <summary>
    /// Get list of all AuthServices (Parent + Child) for customer and role
    /// </summary>
    /// <param name="customer">Id of customer</param>
    /// <param name="role">Full role identifier (e.g. https://api.dlcs.digirati.io/customers/2/roles/clickthrough)</param>
    /// <returns>List of authServices</returns>
    public Task<IEnumerable<AuthService>> GetAuthServicesForRole(int customer, string role);

    /// <summary>
    /// Get single Authservice with specified name.
    /// </summary>
    /// <param name="customer">Id of customer</param>
    /// <param name="name">Name of auth-service</param>
    /// <returns>Matching auth service</returns>
    public Task<AuthService?> GetAuthServiceByName(int customer, string name);

    /// <summary>
    /// Get list of all Roles matching Ids 
    /// </summary>
    /// <param name="customer">Id of customer</param>
    /// <param name="role">Id of roles to find</param>
    /// <returns>Matching role</returns>
    public Task<Role?> GetRole(int customer, string role);

    /// <summary>
    /// Get RoleProvider with specified Id
    /// </summary>
    /// <param name="roleProviderId">Id of roleProvider</param>
    /// <returns>Matching RoleProvider</returns>
    public Task<RoleProvider?> GetRoleProvider(string roleProviderId);
    
    Role CreateRole(string name, int customer, string authServiceId);
    AuthService CreateAuthService(int customerId, string profile, string name, int ttl);
    
    void SaveAuthService(AuthService authService);
    void SaveRole(Role role);
    
    // Below this line reproduces Deliverator IAuthServiceStore
    AuthService Get(string id);
    AuthService GetChild(string id);
    AuthService GetChildByCustomerName(int customer, string name);
    AuthService GetByCustomerName(int customer, string name);
    IEnumerable<AuthService> GetByCustomerRole(int customer, string role);
    IEnumerable<AuthService> GetAll();
    int CountByCustomer(int customer);
    IEnumerable<AuthService> GetByCustomer(int customer, int skip = -1, int take = -1);
    void Put(AuthService authService); // implemented as SaveAuthService
    void Remove(string id);
}