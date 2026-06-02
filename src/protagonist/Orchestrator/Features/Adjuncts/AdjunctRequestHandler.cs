using System.Net;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;

namespace Orchestrator.Features.Adjuncts;

public class AdjunctRequestHandler(
    ILogger<AdjunctRequestHandler> logger,
    AssetRequestProcessor assetRequestProcessor,
    IStorageKeyGenerator storageKeyGenerator,
    S3ProxyPathGenerator proxyPathGenerator,
    IAssetPathGenerator assetPathGenerator,
    IOptions<OrchestratorSettings> orchestratorOptions)
{
    /// <summary>
    /// Handle /adjuncts/ request, returning object detailing operation that should be carried out.
    /// </summary>
    /// <param name="httpContext">Incoming <see cref="HttpContext"/> object</param>
    /// <returns><see cref="IProxyActionResult"/> object containing downstream target</returns>
    public async Task<IProxyActionResult> HandleRequest(HttpContext httpContext)
    {
        var (adjunctRequest, statusCode) =
            await assetRequestProcessor.TryGetAssetDeliveryRequest<AdjunctDeliveryRequest>(httpContext);

        if (statusCode.HasValue || adjunctRequest == null)
        {
            return new StatusCodeResult(statusCode ?? HttpStatusCode.InternalServerError);
        }

        if (adjunctRequest.AdjunctId is not { Length: > 0 })
        {
            logger.LogInformation("AdjunctRequestHandler.HandleRequest called with invalid or missing adjunctId.");
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }
        var orchestrationAdjunct = await assetRequestProcessor.GetAdjunct(httpContext, adjunctRequest);
        if (orchestrationAdjunct == null)
        {
            logger.LogDebug("Request for {Path} adjunct not found", httpContext.Request.Path);
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }
        
        var proxyTarget = GetRequestedAdjunctLocation(adjunctRequest, orchestrationAdjunct);
        if (proxyTarget == null)
        {
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }
        
        // TBD - AUTH

        if (httpContext.Request.Method == "HEAD")
        {
            // quit with success as we've done all we need to
            return new StatusCodeResult(HttpStatusCode.OK);
        }

        if (orchestrationAdjunct.IIIFLink is IIIFLinkType.Annotations)
        {
            return GetIdRewriteResult(adjunctRequest, proxyTarget, orchestrationAdjunct);
        }

        var proxyPath = proxyPathGenerator.GetProxyPath(proxyTarget, !orchestrationAdjunct.OptimisedOrigin ?? true);
        var proxyActionResult = new ProxyActionResult(ProxyDestination.S3, orchestrationAdjunct.RequiresAuth, proxyPath);
        proxyActionResult.Headers.Add("Content-Type", orchestrationAdjunct.MediaType!.Value);
        return proxyActionResult;
    }
    
    private IdRewriteProxyActionResult GetIdRewriteResult(AdjunctDeliveryRequest adjunctRequest,
        ObjectInBucket proxyTarget, OrchestrationAdjunct orchestrationAdjunct)
    {
        var newId = assetPathGenerator.GetFullPathForRequest(new BasicPathElements
        {
            RoutePrefix = AdjunctRouteHandlers.RoutePrefix,
            CustomerPathValue = adjunctRequest.CustomerPathValue,
            Space = adjunctRequest.Space,
            AssetPath = $"{adjunctRequest.AssetId}/{adjunctRequest.AdjunctId}",
        }, includeQueryParams: false);

        var result = new IdRewriteProxyActionResult(proxyTarget, newId)
        {
            MaxSizeBytes = orchestratorOptions.Value.MaxAdjunctSizeBytes
        };
        result.Headers.Add("Content-Type", orchestrationAdjunct.MediaType!.Value);
        return result;
    }

    private ObjectInBucket? GetRequestedAdjunctLocation(AdjunctDeliveryRequest adjunctRequest, OrchestrationAdjunct orchestrationAdjunct)
    {
        ObjectInBucket fileLocation;
        if (orchestrationAdjunct.OptimisedOrigin == true)
        {
            var parsedLocation = RegionalisedObjectInBucket.Parse(orchestrationAdjunct.Origin!);

            if (parsedLocation == null)
            {
                logger.LogWarning("Could not parse '{Origin}' to serve file for {Desc}", orchestrationAdjunct.Origin,
                    orchestrationAdjunct.Identifier());
                return null;
            }

            fileLocation = parsedLocation;
        }
        else
        {
            fileLocation = storageKeyGenerator.GetStoredAdjunctLocation(adjunctRequest.GetAssetId(), adjunctRequest.AdjunctId!);
        }
        
        return fileLocation;
    }
}
