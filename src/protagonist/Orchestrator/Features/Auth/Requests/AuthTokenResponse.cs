namespace Orchestrator.Features.Auth.Requests
{
    public class AuthTokenResponse
    {
        public bool CookieCreated { get; private set; }

        public static AuthTokenResponse Fail() => new() { CookieCreated = false };
        
        public static AuthTokenResponse Success() => new() { CookieCreated = true };
    }
}