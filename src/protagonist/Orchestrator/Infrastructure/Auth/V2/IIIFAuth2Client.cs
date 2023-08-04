using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;

namespace Orchestrator.Infrastructure.Auth.V2;

/// <summary>
/// Client for interactions with IIIF Authorization Flow 2 services
/// </summary>
public class IIIFAuth2Client : IIIIFAuthBuilder
{
    private readonly HttpClient httpClient;
    private readonly ILogger<IIIFAuth2Client> logger;

    public IIIFAuth2Client(HttpClient httpClient, ILogger<IIIFAuth2Client> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }
    
    public async Task<IService?> GetAuthServicesForAsset(AssetId assetId, List<string> roles, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Getting auth 2 services description for {AssetId}, {@Roles}", assetId, roles);
        var path = GetServicesDescriptionPath(assetId, roles);

        try
        {
            await using var authServices = await httpClient.GetStreamAsync(path, cancellationToken);
            var authProbeService = authServices.FromJsonStream<AuthProbeService2>();
            return authProbeService;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting IIIF Auth2 Services for {AssetId}", assetId);
            return null;
        }
    }

    private static string GetServicesDescriptionPath(AssetId assetId, IList<string> roles)
    {
        var rolesString = roles.Count == 1 ? roles[0] : string.Join(",", roles);
        var path = $"services/{assetId}?roles={rolesString}";
        return path;
    }
}