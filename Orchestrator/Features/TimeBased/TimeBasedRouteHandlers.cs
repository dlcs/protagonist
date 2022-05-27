using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.ReverseProxy;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Features.TimeBased
{
    /// <summary>
    /// Route-to-code handlers for /iiif-av/ paths
    /// </summary>
    public static class TimeBasedRouteHandlers
    {
        private static readonly HttpMessageInvoker HttpClient;
        private static readonly HttpTransformer DefaultTransformer;
        private static readonly ForwarderRequestConfig RequestOptions;

        static TimeBasedRouteHandlers()
        {
            // TODO - should this be shared by AV + Image handling?
            HttpClient = new HttpMessageInvoker(new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip,
                UseCookies = false
            });

            DefaultTransformer = HttpTransformer.Default;
            RequestOptions = new ForwarderRequestConfig {ActivityTimeout = TimeSpan.FromSeconds(60)};
        }
        
        /// <summary>
        /// Add endpoint mappings for /iiif-av/ paths
        /// </summary>
        /// <param name="endpoints">Current <see cref="IEndpointRouteBuilder"/> object.</param>
        public static void MapTimeBasedHandling(this IEndpointRouteBuilder endpoints)
        {
            var requestHandler = endpoints.GetRequiredService<TimeBasedRequestHandler>();
            var forwarder = endpoints.GetRequiredService<IHttpForwarder>();
            var logger = endpoints.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(TimeBasedRouteHandlers));

            endpoints.Map("/iiif-av/{customer}/{space}/{image}/{**assetRequest}", async httpContext =>
            {
                logger.LogDebug("Handling request '{Path}'", httpContext.Request.Path);
                var proxyResponse = await requestHandler.HandleRequest(httpContext);
                await ProcessResponse(logger, httpContext, forwarder, proxyResponse);
            });
        }

        private static async Task ProcessResponse(ILogger logger, HttpContext httpContext, IHttpForwarder forwarder,
            IProxyActionResult proxyActionResult)
        {
            if (proxyActionResult is StatusCodeResult statusCodeResult)
            {
                httpContext.Response.StatusCode = (int) statusCodeResult.StatusCode;
                foreach (var header in statusCodeResult.Headers)
                {
                    httpContext.Response.Headers.Add(header);
                }
                return;
            }
            
            var proxyAction = proxyActionResult as ProxyActionResult; 
            await ProxyRequest(logger, httpContext, forwarder, proxyAction);
        }

        private static async Task ProxyRequest(ILogger logger, HttpContext httpContext, IHttpForwarder forwarder,
            ProxyActionResult proxyAction)
        {
            if (proxyAction.Target != ProxyDestination.S3)
            {
                logger.LogError("Found unexpected proxyTarget '{TargetCluster}' - only S3 supported",
                    proxyAction.Target);
                httpContext.Response.StatusCode = 502;
                return;
            }
            
            var transformer = proxyAction.HasPath
                ? new PathRewriteTransformer(proxyAction, true)
                : DefaultTransformer;

            var error = await forwarder.SendAsync(httpContext, proxyAction.Path!, HttpClient, RequestOptions,
                transformer);

            // Check if the proxy operation was successful
            if (error != ForwarderError.None)
            {
                var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
                logger.LogError(errorFeature.Exception!, "Error in iiif-av direct handler for {Path}",
                    httpContext.Request.Path);
            }
        }
    }
}