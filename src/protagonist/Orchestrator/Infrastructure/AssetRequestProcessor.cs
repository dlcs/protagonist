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
public class AssetRequestProcessor
{
    private readonly ILogger<AssetRequestProcessor> logger;
    private readonly IAssetTracker assetTracker;
    private readonly IAssetDeliveryPathParser assetDeliveryPathParser;

    public AssetRequestProcessor(
        ILogger<AssetRequestProcessor> logger,
        IAssetTracker assetTracker,
        IAssetDeliveryPathParser assetDeliveryPathParser)
    {
        this.logger = logger;
        this.assetTracker = assetTracker;
        this.assetDeliveryPathParser = assetDeliveryPathParser;
    }

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
        var imageId = assetRequest.GetAssetId();
        var asset = await assetTracker.GetOrchestrationAsset<T>(imageId);
        
        if (asset != null) httpContext.Response.SetAssetIdResponseHeader(imageId);
        return asset;
    }
}