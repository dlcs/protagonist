using System.Net;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orchestrator.ReverseProxy;

namespace Orchestrator.AV
{
    /// <summary>
    /// Reverse-proxy routing logic for /iiif-av/ requests 
    /// </summary>
    public class AVRequestHandler : RequestHandlerBase
    {
        public AVRequestHandler(
            ILogger<AVRequestHandler> logger,
            IAssetRepository assetRepository,
            IAssetDeliveryPathParser assetDeliveryPathParser) : base(logger, assetRepository, assetDeliveryPathParser)
        {
        }

        /// <summary>
        /// Handle /iiif-img/ request, returning object detailing operation that should be carried out.
        /// </summary>
        /// <param name="httpContext">Incoming <see cref="HttpContext"/> object</param>
        /// <returns><see cref="IProxyActionResult"/> object containing downstream target</returns>
        public async Task<IProxyActionResult> HandleRequest(HttpContext httpContext)
        {
            Logger.LogDebug("Handling request for {Path}", httpContext.Request.Path);
            
            var (assetRequest, statusCode) = await TryGetAssetDeliveryRequest<ImageAssetDeliveryRequest>(httpContext);
            if (statusCode.HasValue || assetRequest == null)
            {
                return new StatusCodeProxyResult(statusCode ?? HttpStatusCode.InternalServerError);
            }

            // If "HEAD" then add CORS - is this required here?
            var asset = await GetAsset(assetRequest);
            if (!DoesAssetRequireAuth(asset))
            {
                Logger.LogDebug("No auth for {Path}, proxying to orchestrator", httpContext.Request.Path);
                return new ProxyActionResult(ProxyTo.Orchestrator);
            }
            
            // Save Range header if exists and set on way out - does YARP handle?
            
            /*
             * If requiresAuth
             * Map customer to Id
             *
             * Look for authCookie or bearer token
             *
             * If no bearer or auth token then 403
             *
             * If token then downstream /auth/service/authPasses/ with cookie
             *   If fail 403
             * If bearer then downstream /auth/service/authPasses/ with header
             *   If fail 403
             *
             * If HEAD, 200
             * proxyPass to S3
             */

            return new ProxyActionResult(ProxyTo.CachingProxy);
        }
    }
}