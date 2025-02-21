using System.Linq;
using System.Security.Claims;

namespace DLCS.Web.Auth;

/// <summary>
/// A collection of extension methods for dealing with <see cref="ClaimsPrincipal"/>
/// </summary>
public static class ClaimsPrincipalUtils
{
    public class Claims
    {
        /// <summary>
        /// Name of claim containing CustomerId.
        /// </summary>
        public static string Customer = "Customer";
    
        /// <summary>
        /// Name of claim containing api basic auth credentials.
        /// </summary>
        public static string ApiCredentials = "ApiCredentials";    
    }

    /// <summary>
    /// Collection of roles associated with Claims
    /// </summary>
    public class Roles
    {
        public const string Customer = "Customer";
        public const string Admin = "Admin";
    }
    
    /// <summary>
    /// Get CustomerId value from principal claims, if present.
    /// </summary>
    public static int? GetCustomerId(this ClaimsPrincipal claimsPrincipal)
    {
        var customerClaim = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == Claims.Customer);
        if (customerClaim == null) return null;
        
        return int.TryParse(customerClaim.Value, out int customerId) ? customerId : null;
    }

    /// <summary>
    /// Get Api basic auth credentials value from principal claims, if present.
    /// </summary>
    public static string? GetApiCredentials(this ClaimsPrincipal claimsPrincipal)
        => claimsPrincipal.GetClaimValue(Claims.ApiCredentials);
    
    /// <summary>
    /// Get User Id value from claim. 
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal claimsPrincipal)
        => claimsPrincipal.GetClaimValue(ClaimTypes.NameIdentifier);
    
    /// <summary>
    /// Get first value of claim with specified type
    /// </summary>
    public static string? GetClaimValue(this ClaimsPrincipal claimsPrincipal, string claimType)
    {
        var customerClaim = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == claimType);
        return customerClaim?.Value;
    }

    /// <summary>
    /// Shortcut extension for testing if user is admin
    /// </summary>
    /// <param name="claimsPrincipal"></param>
    /// <returns></returns>
    public static bool IsAdmin(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.IsInRole(Roles.Admin);
    }
    
    
}