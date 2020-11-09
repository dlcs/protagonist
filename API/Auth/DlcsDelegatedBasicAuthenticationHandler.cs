using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using DLCS.Web.Requests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Auth
{
    /// <summary>
    /// AuthenticationHandler that hands off calls to DLCS for carrying out authentication.
    /// </summary>
    /// <remarks>This is temporary and will be replaced in the future by an implementation that has auth logic</remarks>
    public class DlcsDelegatedBasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationOptions>
    {
        private readonly IHttpClientFactory httpClientFactory;
        
        private const string AuthHeader = "Authorization";
        private const string BasicScheme = "Basic";
        
        public DlcsDelegatedBasicAuthenticationHandler(
            IOptionsMonitor<BasicAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IHttpClientFactory httpClientFactory)
            : base(options, logger, encoder, clock)
        {
            this.httpClientFactory = httpClientFactory;
        }

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

            if (!BasicScheme.Equals(headerValue.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                // Not Basic authentication header
                return AuthenticateResult.NoResult();
            }
            
            if (!Request.RouteValues.TryGetValue("customerId", out var customerIdRouteVal))
            {
                Logger.LogInformation($"Unable to identify customerId in auth request to {Request.Path}");
                return AuthenticateResult.NoResult();
            }
            
            var customerId = customerIdRouteVal.ToString();;
            
            var userAndPassword = Encoding.UTF8.GetString(Convert.FromBase64String(headerValue.Parameter));
            string[] cred = userAndPassword.Split(':');
            
            var user = new {Name = cred[0], Pass = cred[1]};
            if (!await IsValidUser(user.Name, user.Pass, customerId))
            {
                return AuthenticateResult.Fail("Invalid username or password");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Name),
                new Claim("Customer", customerId), 
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
        
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{Options.Realm}\"";
            return base.HandleChallengeAsync(properties);
        }

        private async Task<bool> IsValidUser(string name, string pass, string customerId)
        {
            // Parse the CustomerId out of this.
            var delegatePath = $"/customers/{customerId}"; 

            // make a request to DLCS and verify the result received
            var httpClient = httpClientFactory.CreateClient("dlcs-api");
            var request = new HttpRequestMessage(HttpMethod.Get, delegatePath);
            request.Headers.AddBasicAuth(name, pass);
            var response = await httpClient.SendAsync(request);

            return response.StatusCode == HttpStatusCode.OK;
        }
    }
}