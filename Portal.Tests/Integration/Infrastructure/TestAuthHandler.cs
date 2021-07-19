using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Portal.Tests.Integration.Infrastructure
{
    public static class TestAuthHandlerX
    {
        public static HttpClient AsAdmin(this HttpClient client, int customer = 2)
        {
            client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue($"admin|{customer}");
            return client;
        }
        
        public static HttpClient AsCustomer(this HttpClient client, int customer = 2)
        {
            client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue($"user|{customer}");
            return client;
        }
    }
    
    /// <summary>
    /// Authentication Handler to make testing easier.
    /// </summary>
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
            UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }
        
        private const string AuthHeader = "Authorization";

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(AuthHeader))
            {
                // Authorization header not in request
                return AuthenticateResult.NoResult();
            }
            
            if (!AuthenticationHeaderValue.TryParse(Request.Headers[AuthHeader],
                out AuthenticationHeaderValue headerValue))
            {
                // Invalid Authorization header
                return AuthenticateResult.NoResult();
            }

            bool isAdmin = headerValue.ToString().StartsWith("admin");
            
            var claims = new List<Claim>
            {
                new (ClaimTypes.Name, "test@example.com"),
                new (ClaimTypes.NameIdentifier, "1"),
                new (ClaimsPrincipalUtils.Claims.Customer, headerValue.ToString().Split("|")[^1]),
                new (ClaimTypes.Role, ClaimsPrincipalUtils.Roles.Customer),
                new (ClaimsPrincipalUtils.Claims.ApiCredentials, "basicAuth")
            };

            if (isAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, ClaimsPrincipalUtils.Roles.Admin));
            }
            
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            var result = AuthenticateResult.Success(ticket);
            return result;
        }
    }
}