using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Orchestrator.Features.Auth.Requests
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
        private readonly SessionAuthService sessionAuthService;
        private readonly AuthCookieManager authCookieManager;

        public IssueAuthTokenHandler(
            SessionAuthService sessionAuthService,
            AuthCookieManager authCookieManager)
        {
            this.sessionAuthService = sessionAuthService;
            this.authCookieManager = authCookieManager;
        }
        
        public async Task<AuthTokenResponse> Handle(IssueAuthToken request, CancellationToken cancellationToken)
        {
            // Get authToken for user
            // TODO - allow a user to have an existing session token
            var authToken =
                await sessionAuthService.CreateAuthTokenForRole(request.CustomerId, request.AuthServiceName);
            
            if (authToken == null) return AuthTokenResponse.Fail();

            authCookieManager.SetCookieInResponse(authToken);
            return AuthTokenResponse.Success();
        }

        
    }

    public class AuthTokenResponse
    {
        public bool CookieCreated { get; private set; }

        public static AuthTokenResponse Fail() => new() { CookieCreated = false };
        
        public static AuthTokenResponse Success() => new() { CookieCreated = true };
    }
}