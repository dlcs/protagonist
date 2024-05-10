using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.ReverseProxy;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Orchestrator.Features.Images;

/// <summary>
/// Route-to-code handlers for /iiif-img/ paths
/// </summary>
public static class ImageRouteHandlers
{
    private static readonly HttpMessageInvoker HttpClient;
    private static readonly ForwarderRequestConfig DefaultRequestOptions; 

    static ImageRouteHandlers()
    {
        HttpClient = new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip,
            UseCookies = false,
            ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current)
        });
        
        DefaultRequestOptions = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(60) };
    }

    /// <summary>
    /// Add endpoint mappings for /iiif-img/ paths
    /// </summary>
    /// <param name="endpoints">Current <see cref="IEndpointRouteBuilder"/> object.</param>
    public static void MapImageHandling(this IEndpointRouteBuilder endpoints)
    {
        var requestHandler = endpoints.GetRequiredService<ImageRequestHandler>();
        var forwarder = endpoints.GetRequiredService<IHttpForwarder>();
        var logger = endpoints.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ImageRouteHandlers));
        var orchestrator = endpoints.GetRequiredService<IImageOrchestrator>();
        var destinationSelector = endpoints.GetRequiredService<DownstreamDestinationSelector>();

        /*
         * NOTE: This route also handles /iiif-img/{version}/{customer}/{space}/{image}/{**assetRequest}
         * We don't use route values, instead parse using AssetDeliveryPathParser
         */
        endpoints.MapGet("/iiif-img/{customer}/{space}/{image}/{**assetRequest}", async httpContext =>
        {
            logger.LogDebug("Handling request '{Path}'", httpContext.Request.Path);
            var proxyResponse = await requestHandler.HandleRequest(httpContext);
            await ProcessResponse(logger, httpContext, forwarder, proxyResponse, destinationSelector, orchestrator);
        });
    }
    
    private static async Task ProcessResponse(ILogger logger, HttpContext httpContext, IHttpForwarder forwarder,
        IProxyActionResult proxyActionResult, DownstreamDestinationSelector destinationSelector,
        IImageOrchestrator imageOrchestrator)
    {
        if (proxyActionResult is StatusCodeResult statusCodeResult)
        {
            HandleStatusCodeResult(httpContext, statusCodeResult);
            return;
        }

        if (proxyActionResult is ProxyImageServerResult proxyImageServer)
        {
            var orchestrationResult =
                await imageOrchestrator.EnsureImageOrchestrated(proxyImageServer.OrchestrationImage,
                    httpContext.RequestAborted);
            
            if (orchestrationResult == OrchestrationResult.NotFound)
            {
                HandleStatusCodeResult(httpContext, new StatusCodeResult(HttpStatusCode.NotFound));
                return;
            }
            if (orchestrationResult == OrchestrationResult.Error)
            {
                HandleStatusCodeResult(httpContext, new StatusCodeResult(HttpStatusCode.InternalServerError));
                return;
            }
        }

        var proxyAction = proxyActionResult as ProxyActionResult; 
        await ProxyRequest(logger, httpContext, forwarder, destinationSelector, proxyAction);
    }
    
    private static void HandleStatusCodeResult(HttpContext httpContext, StatusCodeResult statusCodeResult)
    {
        httpContext.Response.StatusCode = (int)statusCodeResult.StatusCode;
        foreach (var header in statusCodeResult.Headers)
        {
            httpContext.Response.Headers.Add(header);
        }
    }

    private static async Task ProxyRequest(ILogger logger, HttpContext httpContext, IHttpForwarder forwarder,
        DownstreamDestinationSelector destinationSelector, ProxyActionResult? proxyAction)
    {
        if (!destinationSelector.TryGetCluster(proxyAction.Target, out ClusterState? cluster))
        {
            logger.LogError("Unable to find target cluster {TargetCluster}", proxyAction.Target);
            httpContext.Response.StatusCode = 502;
            return;
        }

        var destination = destinationSelector.GetClusterTarget(httpContext, cluster);

        if (destination == null)
        {
            logger.LogError("No healthy targets for {TargetCluster}", proxyAction.Target);
            httpContext.Response.StatusCode = 502;
            return;
        }

        var root = destination.Model.Config.Address;

        var transformer = new PathRewriteTransformer(proxyAction);
        var requestOptions = GetRequestOptions(cluster);
        var error = await forwarder.SendAsync(httpContext, root, HttpClient, requestOptions, transformer);

        if (error != ForwarderError.None)
        {
            error.HandleProxyError(httpContext, requestOptions, logger);
        }
    }

    private static ForwarderRequestConfig GetRequestOptions(ClusterState clusterState) =>
        clusterState.Model.Config.HttpRequest ?? DefaultRequestOptions;
}