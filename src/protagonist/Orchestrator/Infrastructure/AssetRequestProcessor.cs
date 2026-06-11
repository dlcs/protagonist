using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using DLCS.Core.Exceptions;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Helper utilities for dealing with Asset requests
/// </summary>
public class AssetRequestProcessor(
    ILogger<AssetRequestProcessor> logger,
    IAssetTracker assetTracker,
    IAdjunctTracker adjunctTracker,
    IAssetDeliveryPathParser assetDeliveryPathParser)
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
}
