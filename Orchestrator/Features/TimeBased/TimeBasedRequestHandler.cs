using System.Net;
using System.Threading.Tasks;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;

namespace Orchestrator.Features.TimeBased
{
    /// <summary>
    /// Reverse-proxy routing logic for /iiif-av/ requests 
    /// </summary>
    public class TimeBasedRequestHandler
    {
        private readonly ILogger<TimeBasedRequestHandler> logger;
        private readonly AssetRequestProcessor assetRequestProcessor;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ProxySettings proxySettings;

        public TimeBasedRequestHandler(
            ILogger<TimeBasedRequestHandler> logger,
            AssetRequestProcessor assetRequestProcessor,
            IServiceScopeFactory scopeFactory,
            IOptions<ProxySettings> proxySettings)
        {
            this.logger = logger;
            this.assetRequestProcessor = assetRequestProcessor;
            this.scopeFactory = scopeFactory;
            this.proxySettings = proxySettings.Value;
        }

        /// <summary>
        /// Handle /iiif-img/ request, returning object detailing operation that should be carried out.
        /// </summary>
        /// <param name="httpContext">Incoming <see cref="HttpContext"/> object</param>
        /// <returns><see cref="IProxyActionResult"/> object containing downstream target</returns>
        public async Task<IProxyActionResult> HandleRequest(HttpContext httpContext)
        {
            // TODO - verify RangeRequest
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
            
            var s3Path =
                $"{proxySettings.S3HttpBase}/{proxySettings.StorageBucket}/{assetRequest.GetAssetId()}{assetRequest.TimeBasedRequest}";
            if (!orchestrationAsset.RequiresAuth)
            {
                logger.LogDebug("No auth for {Path}, 302 to S3 object {S3}", httpContext.Request.Path, s3Path);
                return new StatusCodeResult(HttpStatusCode.Redirect).WithHeader("Location", s3Path);
            }

            if (!await IsAuthenticated(assetRequest, orchestrationAsset))
            {
                logger.LogDebug("User not authenticated for {Path}", httpContext.Request.Path);
                return new StatusCodeResult(HttpStatusCode.Unauthorized);
            }

            if (httpContext.Request.Method == "HEAD")
            {
                // quit with success as we've done all we need to
                return new StatusCodeResult(HttpStatusCode.OK);
            }
            
            return new ProxyActionResult(ProxyDestination.S3, orchestrationAsset.RequiresAuth, s3Path);
        }

        private async Task<bool> IsAuthenticated(TimeBasedAssetDeliveryRequest assetRequest, OrchestrationAsset asset)
        {
            // IAssetAccessValidator is in container with a Lifetime.Scope
            using var scope = scopeFactory.CreateScope();
            var assetAccessValidator = scope.ServiceProvider.GetRequiredService<IAssetAccessValidator>();
            var authResult =
                await assetAccessValidator.TryValidate(assetRequest.Customer.Id, asset.Roles, AuthMechanism.All);

            return authResult is AssetAccessResult.Open or AssetAccessResult.Authorized;
        }
    }
}