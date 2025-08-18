using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using DLCS.Repository.Auth;
using DLCS.Web.Requests;
using DLCS.Web.Response;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Features.Auth.Requests;

public class ProcessRoleProviderToken : IRequest<AuthTokenResponse>
{
    public int CustomerId { get; }
    
    public string AuthServiceName { get; }
    
    public string Token { get; }

    public ProcessRoleProviderToken(int customerId, string authServiceName, string token)
    {
        CustomerId = customerId;
        AuthServiceName = authServiceName;
        Token = token;
    }
}

public class ProcessRoleProviderTokenHandler : IRequestHandler<ProcessRoleProviderToken, AuthTokenResponse>
{
    private readonly IRoleProviderService roleProviderService;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IAuthServicesRepository authServicesRepository;
    private readonly ISessionAuthService sessionAuthService;
    private readonly AuthCookieManager authCookieManager;
    private readonly ILogger<ProcessRoleProviderTokenHandler> logger;

    public ProcessRoleProviderTokenHandler(
        IRoleProviderService roleProviderService,
        IHttpClientFactory httpClientFactory,
        IAuthServicesRepository authServicesRepository,
        ISessionAuthService sessionAuthService,
        AuthCookieManager authCookieManager,
        ILogger<ProcessRoleProviderTokenHandler> logger)
    {
        this.roleProviderService = roleProviderService;
        this.httpClientFactory = httpClientFactory;
        this.authServicesRepository = authServicesRepository;
        this.sessionAuthService = sessionAuthService;
        this.authCookieManager = authCookieManager;
        this.logger = logger;
    }
    
    public async Task<AuthTokenResponse> Handle(ProcessRoleProviderToken request, 
        CancellationToken cancellationToken)
    {
        var roleProvider =
            await roleProviderService.GetRoleProviderForAuthService(request.CustomerId, request.AuthServiceName);

        if (roleProvider == null)
        {
            logger.LogInformation("Could not find authService {AuthServiceName} for customer {Customer}",
                request.CustomerId, request.AuthServiceName);
            return AuthTokenResponse.Fail();
        }
        
        var rolesArray = await GetRolesFromRoleProvider(request, roleProvider, cancellationToken);
        if (rolesArray.IsNullOrEmpty())
        {
            return AuthTokenResponse.Fail();
        }
        
        // iterate through roles and get auth-service per role
        var authServices = new List<AuthService>(rolesArray.Count);
        foreach (var role in rolesArray)
        {
            var authService =
                (await authServicesRepository.GetAuthServicesForRole(request.CustomerId, role)).ToList();
            if (authService.IsNullOrEmpty())
            {
                logger.LogWarning("Could not find auth service for role {Role}, customer {Customer}", role,
                    request.CustomerId);
                continue;
            }
            
            // Add the first only, the rest will be nested child services (logout)
            authServices.Add(authService[0]);
        }

        var authToken = await sessionAuthService.CreateAuthTokenForAuthServices(authServices.ToArray());
        
        if (authToken == null) return AuthTokenResponse.Fail();

        authCookieManager.SetCookieInResponse(authToken);
        return AuthTokenResponse.Success();
    }
    
    private async Task<List<string>> GetRolesFromRoleProvider(ProcessRoleProviderToken request,
        RoleProvider roleProvider, CancellationToken cancellationToken)
    {
        try
        {
            var configuration = roleProviderService.GetRoleProviderConfiguration(roleProvider);

            using var client = httpClientFactory.CreateClient($"{request.CustomerId}:{request.AuthServiceName}");

            var httpRequest = GenerateHttpRequestMessage(request, roleProvider, configuration);
            var response = await client.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var rolesArray = await response.ReadAsJsonAsync<List<string>>();
            return rolesArray ?? new List<string>();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HttpException '{StatusCode}' for {RoleProvider}", ex.StatusCode, roleProvider.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unknown exception calling RoleProvider for {RoleProvider}", roleProvider.Id);
        }

        return new List<string>();
    }
    
    private HttpRequestMessage GenerateHttpRequestMessage(ProcessRoleProviderToken request, RoleProvider roleProvider,
        RoleProviderConfiguration? configuration)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, configuration.Roles);
        httpRequest.Content = new FormUrlEncodedContent(new List<KeyValuePair<string?, string?>>
        {
            new("token", request.Token)
        });

        var credentials = roleProviderService.GetCredentialsForRoleProvider(roleProvider);
        if (credentials != null)
        {
            httpRequest.Headers.AddBasicAuth(credentials.User, credentials.Password);
        }

        return httpRequest;
    }
}
