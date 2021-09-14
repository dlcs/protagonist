using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Security;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;

namespace Orchestrator.Features.Auth.Commands
{
    /// <summary>
    /// Issue a new authToken and cookie for specified
    /// </summary>
    public class IssueAuthToken : IRequest<AuthTokenResponse>
    {
        public int CustomerId { get; }
        
        public string AuthServiceName { get; }

        public IssueAuthToken(int customerId, string authServiceName)
        {
            CustomerId = customerId;
            AuthServiceName = authServiceName;
        }
    }
    
    public class IssueAuthTokenHandler : IRequestHandler<IssueAuthToken, AuthTokenResponse>
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly SessionAuthService sessionAuthService;
        private readonly AuthSettings authSettings;

        public IssueAuthTokenHandler(
            IHttpContextAccessor httpContextAccessor,
            SessionAuthService sessionAuthService,
            IOptions<AuthSettings> authSettings)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.sessionAuthService = sessionAuthService;
            this.authSettings = authSettings.Value;
        }
        
        public async Task<AuthTokenResponse> Handle(IssueAuthToken request, CancellationToken cancellationToken)
        {
            // Get authToken for user
            // TODO - allow a user to have an existing session token
            var authToken =
                await sessionAuthService.CreateAuthTokenForRole(request.CustomerId, request.AuthServiceName);
            
            if (authToken == null) return AuthTokenResponse.Fail();

            SetCookie(authToken);
            return AuthTokenResponse.Success();
        }

        private void SetCookie(AuthToken authToken)
        {
            var httpContext = httpContextAccessor.HttpContext;
            var domains = GetCookiedDomainList(httpContext);

            var cookieValue = $"id={authToken.CookieId}";
            var cookieId = string.Format(authSettings.CookieNameFormat, authToken.Customer);

            httpContext.Response.Cookies.Append(cookieId, cookieValue,
                new CookieOptions
                {
                    Domain = domains,
                    Expires = DateTimeOffset.Now.AddSeconds(authToken.Ttl),
                    SameSite = SameSiteMode.None,
                    Secure = true
                });
        }

        private string? GetCookiedDomainList(HttpContext? httpContext)
        {
            var domains = string.Join(",", authSettings.CookieDomains);
            if (authSettings.UseCurrentDomainForCookie)
            {
                domains += $",{httpContext.Request.Host.Host}";
            }

            return domains;
        }
    }

    public class AuthTokenResponse
    {
        public bool CookieCreated { get; private set; }

        public static AuthTokenResponse Fail() => new() { CookieCreated = false };
        
        public static AuthTokenResponse Success() => new() { CookieCreated = true };
    }
}