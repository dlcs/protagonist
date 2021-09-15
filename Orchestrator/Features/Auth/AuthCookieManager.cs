using System;
using DLCS.Repository.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;

namespace Orchestrator.Features.Auth
{
    /// <summary>
    /// A collection of helper utils for dealing with auth cookies.
    /// </summary>
    public class AuthCookieManager
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly AuthSettings authSettings;
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
            => $"id={cookieId}";

        /// <summary>
        /// Get the CookieId from cookieValue
        /// </summary>
        public string GetCookieIdFromValue(string cookieValue)
            => cookieValue[3..];

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

            httpContext.Response.Cookies.Append(cookieId, cookieValue,
                new CookieOptions
                {
                    Domain = domains,
                    Expires = DateTimeOffset.Now.AddSeconds(authToken.Ttl),
                    SameSite = SameSiteMode.None,
                    Secure = true
                });
        }

        private string GetCookieDomainList(HttpContext? httpContext)
        {
            var domains = string.Join(",", authSettings.CookieDomains);
            if (authSettings.UseCurrentDomainForCookie)
            {
                domains = string.IsNullOrEmpty(domains)
                    ? httpContext.Request.Host.Host
                    : $"{domains},{httpContext.Request.Host.Host}";
            }

            return domains;
        }
    }
}