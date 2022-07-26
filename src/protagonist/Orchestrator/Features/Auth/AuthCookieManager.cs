using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Repository.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;

namespace Orchestrator.Features.Auth;

/// <summary>
/// A collection of helper utils for dealing with auth cookies.
/// </summary>
public class AuthCookieManager
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly AuthSettings authSettings;
    private const string CookiePrefix = "id=";
    
    public AuthCookieManager(
        IHttpContextAccessor httpContextAccessor,
        IOptions<AuthSettings> authSettings
        )
    {
        this.httpContextAccessor = httpContextAccessor;
        this.authSettings = authSettings.Value;
    }
    
    /// <summary>
    /// Get the Id of auth cookie for customer
    /// </summary>
    public string GetAuthCookieKey(string cookieNameFormat, int customer)
        => string.Format(cookieNameFormat, customer);

    /// <summary>
    /// Get the cookieValue from CookieId
    /// </summary>
    public string GetCookieValueForId(string cookieId)
        => $"{CookiePrefix}{cookieId}";

    /// <summary>
    /// Get the CookieId from cookieValue
    /// </summary>
    public string? GetCookieIdFromValue(string cookieValue)
        => cookieValue.StartsWith(CookiePrefix) ? cookieValue[3..] : null;

    /// <summary>
    /// Get Cookie for current customer
    /// </summary>
    public string? GetCookieValueForCustomer(int customer)
    {
        var cookieKey = GetAuthCookieKey(authSettings.CookieNameFormat, customer);
        return httpContextAccessor.HttpContext.Request.Cookies.TryGetValue(cookieKey, out var cookieValue)
            ? cookieValue
            : null;
    }

    /// <summary>
    /// Add cookie to current Response object, using details from specified AuthToken
    /// </summary>
    public void SetCookieInResponse(AuthToken authToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var domains = GetCookieDomainList(httpContext);

        var cookieValue = GetCookieValueForId(authToken.CookieId);
        var cookieId = GetAuthCookieKey(authSettings.CookieNameFormat, authToken.Customer);

        foreach (var domain in domains)
        {
            httpContext.Response.Cookies.Append(cookieId, cookieValue,
                new CookieOptions
                {
                    Domain = domain,
                    Expires = DateTimeOffset.Now.AddSeconds(authToken.Ttl),
                    SameSite = SameSiteMode.None,
                    Secure = true
                });
        }
    }

    /// <summary>
    /// Remove cookie for customer from current Response object 
    /// </summary>
    public void RemoveCookieFromResponse(int customerId)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var domains = GetCookieDomainList(httpContext);
        var cookieId = GetAuthCookieKey(authSettings.CookieNameFormat, customerId);
        
        foreach (var domain in domains)
        {
            httpContext.Response.Cookies.Delete(cookieId, new CookieOptions { Domain = domain });
        }
    }

    private IEnumerable<string> GetCookieDomainList(HttpContext? httpContext)
    {
        var domains = authSettings.CookieDomains;
        return authSettings.UseCurrentDomainForCookie
            ? domains.Union(httpContext.Request.Host.Host.AsList())
            : domains;
    }
}