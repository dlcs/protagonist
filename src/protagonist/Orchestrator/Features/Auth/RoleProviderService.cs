using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using DLCS.Repository.Auth;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Orchestrator.Features.Auth;

/// <summary>
/// Implementation of <see cref="IRoleProviderService"/> that uses current host to filter RoleProvider
/// </summary>
public class HttpAwareRoleProviderService : IRoleProviderService
{
    private readonly IAuthServicesRepository authServicesRepository;
    private readonly IHttpContextAccessor httpContextAccessor;

    public HttpAwareRoleProviderService(
        IAuthServicesRepository authServicesRepository,
        IHttpContextAccessor httpContextAccessor)
    {
        this.authServicesRepository = authServicesRepository;
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task<RoleProviderConfiguration?> GetRoleProviderConfiguration(int customerId,
        string authServiceName)
    {
        var roleProvider = await GetRoleProviderForAuthService(customerId, authServiceName);
        if (roleProvider == null) return null;

        return GetRoleProviderConfiguration(roleProvider);
    }
    
    public RoleProviderConfiguration? GetRoleProviderConfiguration(RoleProvider roleProvider)
    {
        var configBlock = RoleProviderConfigBlock.FromBase64Json(roleProvider.Configuration);
        var configuration = configBlock.GetForHost(httpContextAccessor.HttpContext.Request.Host.ToString());
        return configuration;
    }

    public BasicCredentials? GetCredentialsForRoleProvider(RoleProvider roleProvider)
        => roleProvider.Credentials.IsNullOrEmpty()
            ? null
            : JsonConvert.DeserializeObject<BasicCredentials>(roleProvider.Credentials);

    public async Task<RoleProvider?> GetRoleProviderForAuthService(int customerId, string authServiceName)
    {
        // Load auth services for this role
        var authService = await authServicesRepository.GetAuthServiceByName(customerId, authServiceName);
        if (authService == null) return null;

        // Load RoleProvider for auth-service
        var roleProvider = await authServicesRepository.GetRoleProvider(authService.RoleProvider);
        return roleProvider;
    }
}