using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Auth;
using DLCS.Repository.Auth;

namespace Orchestrator.Infrastructure.Auth;

/// <summary>
/// Contains methods for verifying if <see cref="SessionUser"/> can access assets.
/// </summary>
public class AccessChecker
{
    private readonly IAuthServicesRepository authServicesRepository;

    public AccessChecker(IAuthServicesRepository authServicesRepository)
    {
        this.authServicesRepository = authServicesRepository;
    }

    public async Task<bool> CanSessionUserAccessRoles(SessionUser sessionUser, int customer,
        IEnumerable<string> assetRoles)
    {
        var roleIds = assetRoles.ToList();
        
        if (roleIds.IsNullOrEmpty()) return true;
        
        if (!sessionUser.Roles.TryGetValue(customer, out var sessionRoles))
        {
            // SessionUser doesn't have any roles for customer
            return false;
        }

        // Check if SessionUser has authService for each asset role, if they have any role then they can access
        foreach (var roleId in roleIds)
        {
            var role = await authServicesRepository.GetRole(customer, roleId);
            if (role != null && sessionRoles.Contains(role.AuthService)) return true;
        }

        return false;
    }
}