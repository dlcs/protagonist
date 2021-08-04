using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchestrator.ReverseProxy;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Images
{
    /// <summary>
    /// Route-to-code handlers for /iiif-img/ paths
    /// </summary>
    public static class ImageRouteHandlers
    {
        private static readonly HttpMessageInvoker HttpClient;
        private static readonly HttpTransformer DefaultTransformer;
        private static readonly ForwarderRequestConfig RequestOptions;

        static ImageRouteHandlers()
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
            RequestOptions = new ForwarderRequestConfig {Timeout = TimeSpan.FromSeconds(100)};
        }

        /// <summary>
        /// Add endpoint mappings for /iiif-img/ paths
        /// </summary>
        /// <param name="endpoints">Current <see cref="IEndpointRouteBuilder"/> object.</param>
        public static void MapImageHandling(this IEndpointRouteBuilder endpoints)
        {
            var requestHandler = endpoints.ServiceProvider.GetService<ImageRequestHandler>();
            var forwarder = endpoints.ServiceProvider.GetService<IHttpForwarder>();
            var logger = endpoints.ServiceProvider.GetService<ILoggerFactory>()
                .CreateLogger(nameof(ImageRouteHandlers));

            endpoints.Map("/iiif-img/{customer}/{space}/{image}/{**assetRequest}", async httpContext =>
            {
                logger.LogDebug("Handling request '{Path}'", httpContext.Request.Path);
                var proxyResponse = await requestHandler.HandleRequest(httpContext);
                await ProxyRequest(logger, httpContext, forwarder, proxyResponse);
            });
        }

        private static async Task ProxyRequest(ILogger logger, HttpContext httpContext, IHttpForwarder forwarder,
            IProxyActionResult proxyActionResult)
        {
            if (proxyActionResult is StatusCodeProxyResult statusCodeResult)
            {
                httpContext.Response.StatusCode = (int)statusCodeResult.StatusCode;
                return;
            }

            // TODO - pick appropriate target - get from routes/clusters?
            var proxyAction = proxyActionResult as ProxyActionResult; 
            var root = proxyAction.Target switch
            {
                ProxyDestination.Orchestrator => "http://127.0.0.1:5018",
                ProxyDestination.Unknown => "http://127.0.0.1:5018", // this should never happen - Orchestrator?
                ProxyDestination.Thumbs => "http://127.0.0.1:5018",
                ProxyDestination.ImageServer => "http://127.0.0.1:5018",
                ProxyDestination.CachingProxy => "http://127.0.0.1:5018",
                _ => throw new ArgumentOutOfRangeException()
            };

            var transformer = proxyAction.HasPath
                ? new PathRewriteTransformer(proxyAction.Path)
                : DefaultTransformer;
            
            var error = await forwarder.SendAsync(httpContext, root, HttpClient, RequestOptions, transformer);

            // Check if the proxy operation was successful
            if (error != ForwarderError.None)
            {
                var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
                logger.LogError(errorFeature.Exception!, "Error in catch-all handler for {Path}",
                    httpContext.Request.Path);
            }
        }
    }
}