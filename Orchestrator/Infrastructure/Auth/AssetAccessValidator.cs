using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Orchestrator.Features.Auth;

namespace Orchestrator.Infrastructure.Auth
{
    /// <summary>
    /// Contains logic to validate passed BearerTokens and Cookies with a request for an asset.
    /// Setting status code and cookies depending on result of verification.
    /// </summary>
    public class AssetAccessValidator
    {
        private readonly ISessionAuthService sessionAuthService;
        private readonly AccessChecker accessChecker;
        private readonly IHttpContextAccessor httpContextAccessor;

        public AssetAccessValidator(
            ISessionAuthService sessionAuthService,
            AccessChecker accessChecker,
            IHttpContextAccessor httpContextAccessor)
        {
            this.sessionAuthService = sessionAuthService;
            this.accessChecker = accessChecker;
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Validate whether Bearer token associated with provided request has access to the specified roles for
        /// customer.
        /// </summary>
        /// <param name="customer">Current customer</param>
        /// <param name="roles">Roles associated with Asset</param>
        /// <returns><see cref="AssetAccessResult"/> enum representing result of validation</returns>
        public async Task<AssetAccessResult> TryValidateBearerToken(int customer, IEnumerable<string> roles)
        {
            var assetRoles = roles.ToList();
            if (assetRoles.IsNullOrEmpty()) return AssetAccessResult.Open;

            var httpContext = httpContextAccessor.HttpContext.ThrowIfNull(nameof(httpContextAccessor.HttpContext))!;
            
            var bearerToken = GetBearerToken(httpContext.Request);
            if (string.IsNullOrEmpty(bearerToken))
            {
                // No bearer token, 401 but call underlying (e.g. to render info.json)
                return AssetAccessResult.Unauthorized;
            }
            
            // Get the authToken from bearerToken
            var authToken =
                await sessionAuthService.GetAuthTokenForBearerId(customer, bearerToken);

            if (authToken?.SessionUser == null)
            {
                // Bearer token not found, or expired, 401 but call underlying (e.g. to render info.json)
                return AssetAccessResult.Unauthorized;
            }
            
            // Validate current user has access for roles for requested asset
            var canAccess = await accessChecker.CanSessionUserAccessRoles(authToken.SessionUser, customer, assetRoles);

            if (!canAccess)
            {
                return AssetAccessResult.Unauthorized;
            }
            
            return AssetAccessResult.Authorized;
        }

        private string? GetBearerToken(HttpRequest httpRequest)
            => httpRequest.Headers.TryGetValue(HeaderNames.Authorization, out var authHeader)
                ? authHeader.ToString()?[7..] // everything after "Bearer "
                : null;
    }

    /// <summary>
    /// Enum representing various results for attempting to access an asset.
    /// </summary>
    public enum AssetAccessResult
    {
        /// <summary>
        /// Asset is open
        /// </summary>
        Open,
        
        /// <summary>
        /// Asset is restricted and current user does not have appropriate access
        /// </summary>
        Unauthorized,
        
        /// <summary>
        /// Asset is restricted and current user has access
        /// </summary>
        Authorized
    }
}