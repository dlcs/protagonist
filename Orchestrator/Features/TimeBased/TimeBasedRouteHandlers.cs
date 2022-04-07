﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;
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
            var requestHandler = endpoints.ServiceProvider.GetService<TimeBasedRequestHandler>();
            var forwarder = endpoints.ServiceProvider.GetService<IHttpForwarder>();
            var logger = endpoints.ServiceProvider.GetService<ILoggerFactory>()
                .CreateLogger(nameof(TimeBasedRouteHandlers));
            var settings = endpoints.ServiceProvider.GetService<IOptions<ReverseProxySettings>>();

            endpoints.Map("/iiif-av/{customer}/{space}/{image}/{**assetRequest}", async httpContext =>
            {
                logger.LogDebug("Handling request '{Path}'", httpContext.Request.Path);
                var proxyResponse = await requestHandler.HandleRequest(httpContext);
                await ProxyRequest(logger, httpContext, forwarder, proxyResponse, settings);
            });
        }

        private static async Task ProxyRequest(ILogger logger, HttpContext httpContext, IHttpForwarder forwarder,
            IProxyActionResult proxyActionResult, IOptions<ReverseProxySettings> reverseProxySettings)
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
            var root = proxyAction.Target != ProxyDestination.S3
                ? reverseProxySettings.Value.GetAddressForProxyTarget(proxyAction.Target).ToString()
                : proxyAction.Path;
            
            var transformer = proxyAction.HasPath
                ? new PathRewriteTransformer(proxyAction, proxyAction.Target == ProxyDestination.S3)
                : DefaultTransformer;

            var error = await forwarder.SendAsync(httpContext, root, HttpClient, RequestOptions,
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