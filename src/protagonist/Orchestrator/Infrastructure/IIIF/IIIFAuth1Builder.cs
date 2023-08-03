using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using IIIF;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Settings;
using AuthCookie0 = IIIF.Auth.V0.AuthCookieService;
using AuthCookie1 = IIIF.Auth.V1.AuthCookieService;

namespace Orchestrator.Infrastructure.IIIF;

/// <summary>
/// Implementation of <see cref="IIIIFAuthBuilder"/> for IIIF Auth 0.9 and 1.0
/// </summary>
/// <remarks>This is legacy and will be retired in the future</remarks>
public class IIIFAuth1Builder : IIIIFAuthBuilder
{
    private readonly IAuthServicesRepository authServicesRepository;
    private readonly AuthSettings authSettings;
    private readonly ILogger<IIIFAuth1Builder> logger;

    public IIIFAuth1Builder(
        IAuthServicesRepository authServicesRepository,
        IOptions<AuthSettings> authOptions,
        ILogger<IIIFAuth1Builder> logger)
    {
        this.authServicesRepository = authServicesRepository;
        authSettings = authOptions.Value;
        this.logger = logger;
    }

    /// <summary>
    /// Generate a IIIF <see cref="IService"/> for specified asset.
    /// The 'id'/'@id' parameters will the the name of the auth service only
    /// </summary>
    /// <returns><see cref="IService"/> if found, else null</returns>
    public async Task<IService?> GetAuthServicesForAsset(OrchestrationImage asset,
        CancellationToken cancellationToken = default)
    {
        var assetId = asset.AssetId;
        var authServices = await GetAuthServices(assetId, asset.Roles, cancellationToken);

        if (authServices.IsNullOrEmpty())
        {
            logger.LogWarning("Unable to get auth services for {Asset}", assetId);
            return null;
        }
        
        var authCookieService = authServices[0].ConvertToAuthCookieService(authSettings.SupportedAccessCookieProfiles,
            authSettings.ThrowIfUnsupportedProfile);
        if (authCookieService == null) return null;

        if (authServices.Count == 1) return authCookieService;
        
        var id = authServices[0].Name;
        var childServices = GetChildServices(authServices, id, assetId);
        switch (authCookieService)
        {
            case AuthCookie1 authCookie1:
                authCookie1.Service = childServices;
                break;
            case AuthCookie0 authCookie0:
                authCookie0.Service = childServices;
                break;
        }

        return authCookieService;
    }

    private List<IService> GetChildServices(List<AuthService> authServices, string id, AssetId assetId)
    {
        var services = new List<IService>(authServices.Count - 1);
        foreach (var childAuthService in authServices.Skip(1))
        {
            var subService = childAuthService.ConvertToIIIFChildAuthService(id, authSettings.ThrowIfUnsupportedProfile);
            if (subService != null)
            {
                services.Add(subService);
            }
            else
            {
                logger.LogWarning("Encountered unknown auth service profile '{Profile}' for asset {AssetId}",
                    childAuthService.Profile, assetId);
            }
        }

        return services;
    }

    private async Task<List<AuthService>> GetAuthServices(AssetId assetId, IEnumerable<string> rolesList,
        CancellationToken cancellationToken)
    {
        var authServices = new List<AuthService>();
        foreach (var role in rolesList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            authServices.AddRange(await authServicesRepository.GetAuthServicesForRole(assetId.Customer, role));
        }

        return authServices;
    }
}