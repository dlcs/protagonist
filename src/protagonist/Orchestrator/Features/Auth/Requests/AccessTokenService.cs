using System.Threading;
using System.Threading.Tasks;
using IIIF.Auth.V1.AccessTokenService;
using MediatR;

namespace Orchestrator.Features.Auth.Requests;

public class AccessTokenService : IRequest<AccessTokenServiceResponse>
{
    public int Customer { get; }
    
    public string? MessageId { get; }

    public AccessTokenService(int customer, string? messageId)
    {
        Customer = customer;
        MessageId = messageId;
    }
}

public class AccessTokenServiceHandler : IRequestHandler<AccessTokenService, AccessTokenServiceResponse>
{
    private readonly ISessionAuthService sessionAuthService;
    private readonly AuthCookieManager authCookieManager;

    public AccessTokenServiceHandler(
        ISessionAuthService sessionAuthService,
        AuthCookieManager authCookieManager)
    {
        this.sessionAuthService = sessionAuthService;
        this.authCookieManager = authCookieManager;
    }
    
    public async Task<AccessTokenServiceResponse> Handle(AccessTokenService request, CancellationToken cancellationToken)
    {
        var cookieValue = authCookieManager.GetCookieValueForCustomer(request.Customer);
        if (string.IsNullOrWhiteSpace(cookieValue))
        {
            return AccessTokenServiceResponse.Fail(
                new(AccessTokenErrorConditions.MissingCredentials, "Required cookie missing"));
        }

        var cookieId = authCookieManager.GetCookieIdFromValue(cookieValue);
        if (string.IsNullOrEmpty(cookieId))
        {
            return AccessTokenServiceResponse.Fail(
                new(AccessTokenErrorConditions.InvalidCredentials, "Id not found in cookie"));
        }

        var authToken =
            await sessionAuthService.GetAuthTokenForCookieId(request.Customer, cookieId, cancellationToken);

        if (authToken == null)
        {
            return AccessTokenServiceResponse.Fail(
                new(AccessTokenErrorConditions.InvalidCredentials, "Credentials provided unknown or expired"));
        }
        
        // Set cookie
        authCookieManager.SetCookieInResponse(authToken);
        return AccessTokenServiceResponse.Success(new AccessTokenResponse
        {
            AccessToken = authToken.BearerToken,
            ExpiresIn = authToken.Ttl,
            MessageId = request.MessageId
        });
    }
}

public class AccessTokenServiceResponse
{
    public AccessTokenResponse? Response { get; private init; }
    public AccessTokenError? Error { get; private init; }
    
    public bool IsSuccess { get; private init; }

    public static AccessTokenServiceResponse Success(AccessTokenResponse response)
        => new() { Response = response, IsSuccess = true };

    public static AccessTokenServiceResponse Fail(AccessTokenError error)
        => new() { Error = error, IsSuccess = false };
}