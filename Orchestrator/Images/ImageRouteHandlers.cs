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
using Yarp.ReverseProxy.Configuration;
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
            
            var cc = endpoints.ServiceProvider.GetService<IProxyConfigProvider>();

            endpoints.Map("/iiif-img/{customer}/{space}/{image}/{**catchAll}", async httpContext =>
            {
                logger.LogDebug("Catch-all handling request for {Path}", httpContext.Request.Path);
                var proxyResponse = await requestHandler.HandleRequest(httpContext);
                await ProxyRequest(logger, httpContext, forwarder, proxyResponse);
            });
        }

        private static async Task ProxyRequest(ILogger logger, HttpContext httpContext, IHttpForwarder forwarder,
            ProxyAction proxyAction)
        {
            var root = proxyAction.Target switch
            {
                ProxyTo.Orchestrator => "http://127.0.0.1:8081",
                ProxyTo.Unknown => "http://127.0.0.1:8081", // this should never happen - Orchestrator?
                ProxyTo.Thumbs => "http://127.0.0.1:8081",
                ProxyTo.ImageServer => "http://127.0.0.1:8081",
                ProxyTo.CachingProxy => "http://127.0.0.1:8081",
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