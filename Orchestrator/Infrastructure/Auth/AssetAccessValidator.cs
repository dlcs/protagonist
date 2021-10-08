using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Repository.Security;
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
        private readonly AuthCookieManager authCookieManager;
        private readonly IHttpContextAccessor httpContextAccessor;

        public AssetAccessValidator(
            ISessionAuthService sessionAuthService,
            AccessChecker accessChecker,
            AuthCookieManager authCookieManager,
            IHttpContextAccessor httpContextAccessor)
        {
            this.sessionAuthService = sessionAuthService;
            this.accessChecker = accessChecker;
            this.authCookieManager = authCookieManager;
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Validate whether Bearer token associated with provided request has access to the specified roles for
        /// customer.
        /// </summary>
        /// <param name="customer">Current customer</param>
        /// <param name="roles">Roles associated with Asset</param>
        /// <returns><see cref="AssetAccessResult"/> enum representing result of validation</returns>
        public Task<AssetAccessResult> TryValidateBearerToken(int customer, IEnumerable<string> roles)
            => ValidateAccess(customer, roles, () =>
            {
                var httpContext = httpContextAccessor.HttpContext.ThrowIfNull(nameof(httpContextAccessor.HttpContext))!;

                var bearerToken = GetBearerToken(httpContext.Request);
                return string.IsNullOrEmpty(bearerToken)
                    ? Task.FromResult<AuthToken?>(null)
                    : sessionAuthService.GetAuthTokenForBearerId(customer, bearerToken);
            });

        /// <summary>
        /// Validate whether cookie provided with request has access to the specified roles for customer
        /// </summary>
        /// <param name="customer">Current customer</param>
        /// <param name="roles">Roles associated with Asset</param>
        /// <returns><see cref="AssetAccessResult"/> enum representing result of validation</returns>
        public Task<AssetAccessResult> TryValidateCookie(int customer, IEnumerable<string> roles)
            => ValidateAccess(customer, roles, () =>
            {
                var cookieId = GetCookieId(customer);
                return string.IsNullOrEmpty(cookieId)
                    ? Task.FromResult<AuthToken?>(null)
                    : sessionAuthService.GetAuthTokenForCookieId(customer, cookieId);
            });

        private async Task<AssetAccessResult> ValidateAccess(int customer, IEnumerable<string> roles,
            Func<Task<AuthToken?>> getAuthToken)
        {
            var assetRoles = roles.ToList();
            if (assetRoles.IsNullOrEmpty()) return AssetAccessResult.Open;

            var authToken = await getAuthToken();
            
            if (authToken?.SessionUser == null)
            {
                // Authtoken token not found, or expired
                return AssetAccessResult.Unauthorized;
            }
            
            // Validate current user has access for roles for requested asset
            var canAccess = await accessChecker.CanSessionUserAccessRoles(authToken.SessionUser, customer, assetRoles);
            return canAccess ? AssetAccessResult.Authorized : AssetAccessResult.Unauthorized;
        }

        private string? GetCookieId(int customer)
        {
            var cookieValue = authCookieManager.GetCookieValueForCustomer(customer);
            return string.IsNullOrEmpty(cookieValue) ? null : authCookieManager.GetCookieIdFromValue(cookieValue);
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