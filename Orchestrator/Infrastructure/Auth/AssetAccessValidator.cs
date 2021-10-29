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
    public interface IAssetAccessValidator
    {
        /// <summary>
        /// Validate whether current request has access to the specified roles for customer. This will try to validate
        /// via cookie and fallback to Bearer token.
        /// </summary>
        /// <param name="customer">Current customer</param>
        /// <param name="roles">Roles associated with Asset</param>
        /// <param name="mechanism">Which mechanism to use to authorize user</param>
        /// <returns><see cref="AssetAccessResult"/> enum representing result of validation</returns>
        Task<AssetAccessResult> TryValidate(int customer, IEnumerable<string> roles, AuthMechanism mechanism);
    }

    /// <summary>
    /// Contains logic to validate passed BearerTokens and Cookies with a request for an asset.
    /// Setting status code and cookies depending on result of verification.
    /// </summary>
    public class AssetAccessValidator : IAssetAccessValidator
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

        public Task<AssetAccessResult> TryValidate(int customer, IEnumerable<string> roles, AuthMechanism mechanism)
            => mechanism switch
            {
                AuthMechanism.All => TryValidateAll(customer, roles),
                AuthMechanism.Cookie => TryValidateCookie(customer, roles),
                AuthMechanism.BearerToken => TryValidateBearerToken(customer, roles),
                _ => throw new ArgumentOutOfRangeException(nameof(mechanism), mechanism, null)
            };
        
        private async Task<AssetAccessResult> TryValidateAll(int customer, IEnumerable<string> roles)
        {
            var enumeratedRoles = roles.ToList();
            var validateCookieResult = await TryValidateCookie(customer, enumeratedRoles);
            if (validateCookieResult is AssetAccessResult.Open or AssetAccessResult.Authorized)
            {
                return validateCookieResult;
            }

            return await TryValidateBearerToken(customer, enumeratedRoles);
        }
        
        private Task<AssetAccessResult> TryValidateBearerToken(int customer, IEnumerable<string> roles)
            => ValidateAccess(customer, roles, () =>
            {
                var httpContext = httpContextAccessor.HttpContext.ThrowIfNull(nameof(httpContextAccessor.HttpContext))!;

                var bearerToken = GetBearerToken(httpContext.Request);
                return string.IsNullOrEmpty(bearerToken)
                    ? Task.FromResult<AuthToken?>(null)
                    : sessionAuthService.GetAuthTokenForBearerId(customer, bearerToken);
            }, false);

        private Task<AssetAccessResult> TryValidateCookie(int customer, IEnumerable<string> roles)
            => ValidateAccess(customer, roles, () =>
            {
                var cookieId = GetCookieId(customer);
                return string.IsNullOrEmpty(cookieId)
                    ? Task.FromResult<AuthToken?>(null)
                    : sessionAuthService.GetAuthTokenForCookieId(customer, cookieId);
            }, true);

        private async Task<AssetAccessResult> ValidateAccess(int customer, IEnumerable<string> roles,
            Func<Task<AuthToken?>> getAuthToken, bool setCookieInResponse)
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
            if (canAccess)
            {
                if (setCookieInResponse)
                {
                    authCookieManager.SetCookieInResponse(authToken);
                }

                return AssetAccessResult.Authorized;
            }
            
            return AssetAccessResult.Unauthorized;
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

    /// <summary>
    /// Enum representing different mechanisms for authorising users
    /// </summary>
    public enum AuthMechanism
    {
        /// <summary>
        /// Auth user by cookie provided with request
        /// </summary>
        Cookie,
        
        /// <summary>
        /// Auth user by bearer token provided with request
        /// </summary>
        BearerToken,
        
        /// <summary>
        /// Try all possible methods of validation
        /// </summary>
        All
    }
}