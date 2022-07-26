using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using IIIF;
using IIIF.Auth.V1;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Strings;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;

namespace Orchestrator.Infrastructure.IIIF;

public class IIIFAuthBuilder
{
    private readonly IAuthServicesRepository authServicesRepository;
    private readonly ILogger<IIIFAuthBuilder> logger;

    public IIIFAuthBuilder(IAuthServicesRepository authServicesRepository,
        ILogger<IIIFAuthBuilder> logger)
    {
        this.authServicesRepository = authServicesRepository;
        this.logger = logger;
    }

    /// <summary>
    /// Generate a IIIF <see cref="AuthCookieService"/> for specified asset.
    /// The 'id'/'@id' parameters will the the name of the auth service only
    /// </summary>
    /// <returns><see cref="AuthCookieService"/> if found, else null</returns>
    public async Task<AuthCookieService?> GetAuthCookieServiceForAsset(OrchestrationImage asset,
        CancellationToken cancellationToken = default)
    {
        var assetId = asset.AssetId;
        var authServices = await GetAuthServices(assetId, asset.Roles, cancellationToken);

        if (authServices.IsNullOrEmpty())
        {
            logger.LogWarning("Unable to get auth services for {Asset}", assetId);
            return null;
        }

        var id = authServices[0].Name;

        var parentService = authServices[0];
        var authCookieService = new AuthCookieService(parentService.Profile)
        {
            Id = id,
            Label = new MetaDataValue(parentService.Label),
            Description = new MetaDataValue(parentService.Description),
        };

        if (authServices.Count == 1) return authCookieService;

        authCookieService.Service = GetChildServices(authServices, id, assetId);

        return authCookieService;
    }

    private List<IService> GetChildServices(List<AuthService> authServices, string id, AssetId assetId)
    {
        var services = new List<IService>(authServices.Count - 1);
        foreach (var childAuthService in authServices.Skip(1))
        {
            IService subService;
            switch (childAuthService.Profile)
            {
                case AuthLogoutService.AuthLogout1Profile:
                    subService = new AuthLogoutService
                    {
                        Id = $"{id}/logout" // hmmm
                    };
                    break;
                case AuthTokenService.AuthToken1Profile:
                    subService = new AuthTokenService
                    {
                        Id = childAuthService.Name
                    };
                    break;
                default:
                    logger.LogWarning("Encountered unknown auth service for asset {AssetId}", assetId);
                    throw new ArgumentException($"Unknown AuthService profile type: {childAuthService.Profile}");
            }

            if (childAuthService.Label.HasText())
            {
                (subService as ResourceBase)!.Label = new MetaDataValue(childAuthService.Label);
            }

            if (childAuthService.Description.HasText())
            {
                (subService as ResourceBase)!.Description = new MetaDataValue(childAuthService.Description);
            }

            services.Add(subService);
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