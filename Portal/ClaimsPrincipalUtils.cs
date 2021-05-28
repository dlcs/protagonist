using System.Linq;
using System.Security.Claims;

namespace Portal
{
    /// <summary>
    /// A collection of extension methods for dealing with <see cref="ClaimsPrincipal"/>
    /// </summary>
    public static class ClaimsPrincipalUtils
    {
        /// <summary>
        /// Name of claim containing CustomerId.
        /// </summary>
        public static string CustomerClaim = "Customer";
        
        /// <summary>
        /// Name of claim containing api basic auth credentials.
        /// </summary>
        public static string ApiCredentialsClaim = "ApiCredentials";
        
        /// <summary>
        /// Get CustomerId value from principal claims, if present.
        /// </summary>
        public static int? GetCustomerId(this ClaimsPrincipal claimsPrincipal)
        {
            var customerClaim = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == CustomerClaim);
            if (customerClaim == null) return null;
            
            return int.TryParse(customerClaim.Value, out int customerId) ? customerId : null;
        }
        
        /// <summary>
        /// Get Api basic auth credentials value from principal claims, if present.
        /// </summary>
        public static string? GetApiCredentials(this ClaimsPrincipal claimsPrincipal)
        {
            var customerClaim = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == ApiCredentialsClaim);
            return customerClaim?.Value;
        }
    }
}