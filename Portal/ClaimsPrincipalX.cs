using System.Linq;
using System.Security.Claims;

namespace Portal
{
    /// <summary>
    /// A collection of extension methods for dealing with <see cref="ClaimsPrincipal"/>
    /// </summary>
    public static class ClaimsPrincipalX
    {
        public static int? GetCustomerId(this ClaimsPrincipal claimsPrincipal)
        {
            var customerClaim = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == "Customer");
            if (customerClaim == null) return null;
            
            return int.TryParse(customerClaim.Value, out int customerId) ? customerId : null;
        }
    }
}