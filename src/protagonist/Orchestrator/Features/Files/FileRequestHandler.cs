using System.Net;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Features.TimeBased;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.ReverseProxy;

namespace Orchestrator.Features.Files;

/// <summary>
/// Handling logic for /file/ requests 
/// </summary>
public class FileRequestHandler
{
    private readonly ILogger<TimeBasedRequestHandler> logger;
    private readonly AssetRequestProcessor assetRequestProcessor;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly S3ProxyPathGenerator proxyPathGenerator;

    public FileRequestHandler(
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
    /// Handle /file/ request, returning object detailing operation that should be carried out.
    /// </summary>
    /// <param name="httpContext">Incoming <see cref="HttpContext"/> object</param>
    /// <returns><see cref="IProxyActionResult"/> object containing downstream target</returns>
    public async Task<IProxyActionResult> HandleRequest(HttpContext httpContext)
    {
        var (assetRequest, statusCode) =
            await assetRequestProcessor.TryGetAssetDeliveryRequest<FileAssetDeliveryRequest>(httpContext);
        if (statusCode.HasValue || assetRequest == null)
        {
            return new StatusCodeResult(statusCode ?? HttpStatusCode.InternalServerError);
        }
        
        var orchestrationAsset = await assetRequestProcessor.GetAsset(assetRequest);
        if (orchestrationAsset == null)
        {
            logger.LogDebug("Request for {Path} asset not found", httpContext.Request.Path);
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }
        
        if (!orchestrationAsset.Channels.HasFlag(AvailableDeliveryChannel.File))
        {
            logger.LogDebug("Request for {Path}: asset not available in 'file' channel", httpContext.Request.Path);
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }
        
        var proxyTarget = GetRequestedAssetLocation(assetRequest, orchestrationAsset);
        if (proxyTarget == null)
        {
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }
        
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

        var proxyPath = proxyPathGenerator.GetProxyPath(proxyTarget);
        return new ProxyActionResult(ProxyDestination.S3, orchestrationAsset.RequiresAuth, proxyPath);
    }
    
    private async Task<bool> IsAuthenticated(FileAssetDeliveryRequest assetRequest, OrchestrationAsset asset,
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
    
    private ObjectInBucket? GetRequestedAssetLocation(FileAssetDeliveryRequest assetRequest, OrchestrationAsset orchestrationAsset)
    {
        ObjectInBucket fileLocation;
        if (orchestrationAsset.OptimisedOrigin == true)
        {
            var parsedLocation = RegionalisedObjectInBucket.Parse(orchestrationAsset.Origin!);

            if (parsedLocation == null)
            {
                logger.LogWarning("Could not parse '{Origin}' to serve file for {AssetId}", orchestrationAsset.Origin,
                    orchestrationAsset.AssetId);
                return null;
            }

            fileLocation = parsedLocation;
        }
        else
        {
            fileLocation = storageKeyGenerator.GetStoredOriginalLocation(assetRequest.GetAssetId());
        }
        
        return fileLocation;
    }
}