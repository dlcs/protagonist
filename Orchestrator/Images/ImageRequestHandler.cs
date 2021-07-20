using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Images
{
    public static class ImageRequestHandlers
    {
        private static HttpMessageInvoker httpClient;
        private static HttpTransformer transformer;
        private static ForwarderRequestConfig requestOptions;

        static ImageRequestHandlers()
        {
            httpClient = new HttpMessageInvoker(new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip,
                UseCookies = false
            });

            transformer = HttpTransformer.Default;
            requestOptions = new ForwarderRequestConfig {Timeout = TimeSpan.FromSeconds(100)};
        }
        
        public static void MapImageHandling(this IEndpointRouteBuilder endpoints)
        {
            var forwarder = endpoints.ServiceProvider.GetService<IHttpForwarder>();
            var logger = endpoints.ServiceProvider.GetService<ILoggerFactory>()
                .CreateLogger(nameof(ImageRequestHandlers));

            endpoints.Map("/iiif-img/{customer}/{space}/{image}/{**catchAll}", async httpContext =>
                await CatchAll(logger, httpContext, forwarder)
            );
        }

        private static async Task CatchAll(ILogger logger, HttpContext httpContext, IHttpForwarder forwarder)
        {
            logger.LogDebug("Catch-all handling request for {Path}", httpContext.Request.Path);

            var dlcsContext = httpContext.RequestServices.GetService<DlcsContext>();

            var customer = httpContext.Request.RouteValues["customer"]?.ToString();
            var space = httpContext.Request.RouteValues["space"]?.ToString();
            var image = httpContext.Request.RouteValues["image"]?.ToString();
            var catchAll = httpContext.Request.RouteValues["catchAll"]?.ToString();
            
            // If "HEAD" then add CORS
            
            // Call /requiresAuth/
            var requiresAuth = await ImageRequiresAuth(dlcsContext, customer, space, image);
            if (requiresAuth)
            {
                // TODO - proxy to orchestrator
                logger.LogDebug("Request for {Path} requires auth, proxying to orchestrator", httpContext.Request.Path);
                await ProxyRequest(logger, httpContext, forwarder);
                return;
            }
            
            // TODO - add UV_THUMB_HACK as an appSetting
            if (catchAll == "full/90,/0/default.jpg" && httpContext.Request.QueryString.Value.Contains("t="))
            {
                // TODO - proxy to thumbs
                logger.LogDebug("Request for {Path} looks like UV thumb, proxying to thumbs", httpContext.Request.Path);
                await ProxyRequest(logger, httpContext, forwarder);
                return;
            }
            
            /*
             * MapCustomerToId
             * Parse the size parameter to see if we can handle it with an exact size
             * If so, return S3 URL? (put to /thumbs)
             * Else, proxy Varnish
             */

            // TODO - get the deliverator id from config, not hardcoded. This is currently going to httpHole
            await ProxyRequest(logger, httpContext, forwarder);
        }

        private static async Task ProxyRequest(ILogger logger, HttpContext httpContext, IHttpForwarder forwarder)
        {
            var error = await forwarder.SendAsync(httpContext, "http://127.0.0.1:8081", httpClient,
                requestOptions, transformer);

            // Check if the proxy operation was successful
            if (error != ForwarderError.None)
            {
                var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
                logger.LogError(errorFeature.Exception!, "Error in catch-all handler for {Path}",
                    httpContext.Request.Path);
            }
        }

        private static async Task<bool> ImageRequiresAuth(DlcsContext dbContext, string customer, string space, string image)
        {
            // TODO - cache this. Use a repo?
            var imageId = new AssetImageId(customer, space, image);
            var roles = await dbContext.Images.AsNoTracking()
                .Where(i => i.Id == imageId.ToString())
                .Select(i => i.Roles)
                .SingleAsync();

            return !string.IsNullOrWhiteSpace(roles);
        }
    }
}