using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Repository.Auth;
using DLCS.Web;
using DLCS.Web.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Orchestrator.Features.Auth;

namespace Orchestrator.Infrastructure.Auth;

/// <summary>
/// Contains logic to validate passed BearerTokens and Cookies with a request for an asset.
/// Setting status code and cookies depending on result of verification.
/// </summary>
public class Auth1AccessValidator : IAssetAccessValidator
{
    private readonly ISessionAuthService sessionAuthService;
    private readonly AccessChecker accessChecker;
    private readonly AuthCookieManager authCookieManager;
    private readonly IHttpContextAccessor httpContextAccessor;

    public Auth1AccessValidator(
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

    public Task<AssetAccessResult> TryValidate(AssetId assetId, List<string> roles, AuthMechanism mechanism,
        CancellationToken cancellationToken = default) => mechanism switch
    {
        AuthMechanism.All => TryValidateAll(assetId.Customer, roles),
        AuthMechanism.Cookie => TryValidateCookie(assetId.Customer, roles),
        AuthMechanism.BearerToken => TryValidateBearerToken(assetId.Customer, roles),
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
            var httpContext = httpContextAccessor.SafeHttpContext();

            var bearerTokenHeader = httpContext.Request.GetAuthHeaderValue(AuthenticationHeaderUtils.BearerTokenScheme);
            var bearerToken = bearerTokenHeader?.Parameter;
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