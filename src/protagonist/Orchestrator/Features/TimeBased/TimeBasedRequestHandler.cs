using System;
using System.Net;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.ReverseProxy;

namespace Orchestrator.Features.TimeBased;

/// <summary>
/// Reverse-proxy routing logic for /iiif-av/ requests 
/// </summary>
public class TimeBasedRequestHandler
{
    private readonly ILogger<TimeBasedRequestHandler> logger;
    private readonly AssetRequestProcessor assetRequestProcessor;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly S3ProxyPathGenerator proxyPathGenerator;

    public TimeBasedRequestHandler(
        ILogger<TimeBasedRequestHandler> logger,
        AssetRequestProcessor assetRequestProcessor,
        IServiceScopeFactory scopeFactory,
        IStorageKeyGenerator storageKeyGenerator,
        S3ProxyPathGenerator proxyPathGenerator)
    {
        this.logger = logger;
        this.assetRequestProcessor = assetRequestProcessor;
        this.scopeFactory = scopeFactory;
        this.storageKeyGenerator = storageKeyGenerator;
        this.proxyPathGenerator = proxyPathGenerator;
    }

    /// <summary>
    /// Handle /iiif-av/ request, returning object detailing operation that should be carried out.
    /// </summary>
    /// <param name="httpContext">Incoming <see cref="HttpContext"/> object</param>
    /// <returns><see cref="IProxyActionResult"/> object containing downstream target</returns>
    public async Task<IProxyActionResult> HandleRequest(HttpContext httpContext)
    {
        var (assetRequest, statusCode) =
            await assetRequestProcessor.TryGetAssetDeliveryRequest<TimeBasedAssetDeliveryRequest>(httpContext);
        if (statusCode.HasValue || assetRequest == null)
        {
            return new StatusCodeResult(statusCode ?? HttpStatusCode.InternalServerError);
        }

        // If "HEAD" then add CORS - is this required here?
        var orchestrationAsset = await assetRequestProcessor.GetAsset(assetRequest);
        if (orchestrationAsset == null)
        {
            logger.LogDebug("Request for {Path} asset not found", httpContext.Request.Path);
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }
        
        if (!orchestrationAsset.Channels.HasFlag(AvailableDeliveryChannel.Timebased))
        {
            logger.LogDebug("Request for {Path}: asset not available in 'timebased' channel", httpContext.Request.Path);
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        var proxyTarget = GetRequestedAssetHttpUri(assetRequest);
        if (orchestrationAsset.RequiresAuth)
        {
            if (!await IsAuthenticated(assetRequest, orchestrationAsset, httpContext.Request))
            {
                logger.LogDebug("User not authenticated for {Method} {Path}", httpContext.Request.Method,
                    httpContext.Request.Path);
                return new StatusCodeResult(HttpStatusCode.Unauthorized);
            }
        }
        
        // By here it's either open, or user is authenticated
        if (httpContext.Request.Method == "HEAD")
        {
            // quit with success as we've done all we need to
            return new StatusCodeResult(HttpStatusCode.OK);
        }
        
        var proxyPath = proxyPathGenerator.GetProxyPath(proxyTarget, true);
        return new ProxyActionResult(ProxyDestination.S3, orchestrationAsset.RequiresAuth, proxyPath);
    }

    private ObjectInBucket GetRequestedAssetHttpUri(TimeBasedAssetDeliveryRequest assetRequest)
    {
        var location =
            storageKeyGenerator.GetTimebasedAssetLocation(assetRequest.GetAssetId(), assetRequest.TimeBasedRequest);
        return location;
    }

    private async Task<bool> IsAuthenticated(TimeBasedAssetDeliveryRequest assetRequest, OrchestrationAsset asset,
        HttpRequest httpRequest)
    {
        // IAssetAccessValidator is in container with a Lifetime.Scope
        using var scope = scopeFactory.CreateScope();
        var assetAccessValidator = scope.ServiceProvider.GetRequiredService<IAssetAccessValidator>();

        // We can get HEAD or GET requests here, for GET requests we only check Cookies, bearer tokens are ignored
        var authMechanism = httpRequest.Method == "GET" ? AuthMechanism.Cookie : AuthMechanism.All;
        logger.LogDebug("Authenticating request for {Method} {Path} via {Mechanism}", httpRequest.Method,
            httpRequest.Path, authMechanism);
        var authResult =
            await assetAccessValidator.TryValidate(assetRequest.Customer.Id, asset.Roles, authMechanism);

        return authResult is AssetAccessResult.Open or AssetAccessResult.Authorized;
    }
}