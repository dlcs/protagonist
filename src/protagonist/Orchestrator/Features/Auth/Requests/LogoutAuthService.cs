using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Auth;
using DLCS.Repository.Auth;
using MediatR;

namespace Orchestrator.Features.Auth.Requests;

/// <summary>
/// Log user out of auth service for specified customer
/// </summary>
public class LogoutAuthService : IRequest<Uri?>
{
    public int CustomerId { get; }
    
    public string AuthServiceName { get; }

    public LogoutAuthService(int customerId, string authServiceName)
    {
        CustomerId = customerId;
        AuthServiceName = authServiceName;
    }
}

public class LogoutAuthServiceHandler : IRequestHandler<LogoutAuthService, Uri?>
{
    private readonly IAuthServicesRepository authServicesRepository;
    private readonly IRoleProviderService roleProviderService;
    private readonly ISessionAuthService sessionAuthService;
    private readonly AuthCookieManager authCookieManager;

    public LogoutAuthServiceHandler(
        IAuthServicesRepository authServicesRepository,
        IRoleProviderService roleProviderService,
        ISessionAuthService sessionAuthService,
        AuthCookieManager authCookieManager)
    {
        this.authServicesRepository = authServicesRepository;
        this.roleProviderService = roleProviderService;
        this.sessionAuthService = sessionAuthService;
        this.authCookieManager = authCookieManager;
    }
    
    public async Task<Uri?> Handle(LogoutAuthService request, CancellationToken cancellationToken)
    {
        var cookieId = GetCookieId(request.CustomerId);
        if (string.IsNullOrEmpty(cookieId)) return null;

        var authService =
            await authServicesRepository.GetAuthServiceByName(request.CustomerId, request.AuthServiceName);
        if (authService == null) return null;

        var authToken =
            await sessionAuthService.RemoveAuthServiceFromToken(request.CustomerId, cookieId,
                authService.Id, cancellationToken);

        if (authToken == null || !authToken.SessionUser.Roles.ContainsKey(request.CustomerId))
        {
            // User no longer has a session, or the session doesn't have any access for customer
            authCookieManager.RemoveCookieFromResponse(request.CustomerId);
        }

        var config =
            await roleProviderService.GetRoleProviderConfiguration(request.CustomerId, request.AuthServiceName);

        return config?.Logout != null ? config.Logout : null;
    }
    
    private string? GetCookieId(int customer)
    {
        var cookieValue = authCookieManager.GetCookieValueForCustomer(customer);
        return string.IsNullOrEmpty(cookieValue) ? null : authCookieManager.GetCookieIdFromValue(cookieValue);
    }
}