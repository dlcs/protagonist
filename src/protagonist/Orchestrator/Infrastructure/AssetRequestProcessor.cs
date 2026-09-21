using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using DLCS.Core.Exceptions;
using DLCS.Core.Types;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.Auth;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Helper utilities for dealing with Asset requests
/// </summary>
public class AssetRequestProcessor(
    ILogger<AssetRequestProcessor> logger,
    IAssetTracker assetTracker,
    IAdjunctTracker adjunctTracker,
    IAssetDeliveryPathParser assetDeliveryPathParser,
    IServiceScopeFactory scopeFactory)
{
    /// <summary>
    /// Try and parse current asset request, handling possible errors that may occur
    /// </summary>
    /// <param name="httpContext"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>Parsed asset request, if successful. Else error status code.</returns>
    public async Task<(T? assetRequest, HttpStatusCode? statusCode)> TryGetAssetDeliveryRequest<T>(
        HttpContext httpContext) where T : BaseAssetRequest, new()
    {
        try
        {
            var assetRequest =
                await assetDeliveryPathParser.ParseForHttp<T>(httpContext.Request.Path);
            
            return (assetRequest, null);
        }
        catch (HttpException ex)
        {
            return (null, ex.StatusCode);
        }
    }

    /// <summary>
    /// Get cached <see cref="OrchestrationAsset"/>, setting x-asset-id header in response if found
    /// </summary>
    public async Task<T?> GetAsset<T>(HttpContext httpContext, BaseAssetRequest assetRequest)
        where T : OrchestrationAsset
    {
        var assetId = assetRequest.GetAssetId();
        var asset = await assetTracker.GetOrchestrationAsset<T>(assetId);
        
        if (asset != null)
        {
            httpContext.Response.SetAssetIdResponseHeader(assetId);
        }

        return asset;
    }

    public async Task<OrchestrationAdjunct?> GetAdjunct(HttpContext httpContext, AdjunctDeliveryRequest adjunctRequest)
    {
        // Checked in AdjunctRequestHandler
        Debug.Assert(adjunctRequest.AdjunctId != null, "adjunctRequest.AdjunctId != null");
        
        var assetId = adjunctRequest.GetAssetId();
        var adjunct = await adjunctTracker.GetOrchestrationAdjunct(adjunctRequest.AdjunctId, assetId);
        
        if (adjunct != null)
        {
            httpContext.Response.SetAssetIdResponseHeader(assetId);
        }

        return adjunct;
    }

    /// <summary>
    /// Determine whether the current request is permitted to access an asset (or adjunct) with the given roles.
    /// </summary>
    /// <param name="assetId">AssetId the roles belong to</param>
    /// <param name="roles">Roles associated with the asset/adjunct being requested</param>
    /// <param name="httpRequest">Current <see cref="HttpRequest"/>, used to determine auth mechanism + for logging</param>
    public async Task<bool> IsAuthenticated(AssetId assetId, IReadOnlyList<string> roles, HttpRequest httpRequest)
    {
        // IAssetAccessValidator is in container with a Lifetime.Scope
        using var scope = scopeFactory.CreateScope();
        var assetAccessValidator = scope.ServiceProvider.GetRequiredService<IAssetAccessValidator>();

        // We can get HEAD or GET requests here, for GET requests we only check Cookies, bearer tokens are ignored
        var authMechanism = httpRequest.Method == "GET" ? AuthMechanism.Cookie : AuthMechanism.All;
        logger.LogDebug("Authenticating request for {Method} {Path} via {Mechanism}", httpRequest.Method,
            httpRequest.Path, authMechanism);
        var authResult = await assetAccessValidator.TryValidate(assetId, roles, authMechanism);

        return authResult is AssetAccessResult.Open or AssetAccessResult.Authorized;
    }
}
