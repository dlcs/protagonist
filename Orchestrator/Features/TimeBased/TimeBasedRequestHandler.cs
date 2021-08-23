using System.Net;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Web.Auth;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;

namespace Orchestrator.Features.TimeBased
{
    /// <summary>
    /// Reverse-proxy routing logic for /iiif-av/ requests 
    /// </summary>
    public class TimeBasedRequestHandler : RequestHandlerBase
    {
        private readonly IDeliveratorClient deliveratorClient;
        private readonly ProxySettings proxySettings;

        public TimeBasedRequestHandler(
            ILogger<TimeBasedRequestHandler> logger,
            IAssetTracker assetTracker,
            IAssetDeliveryPathParser assetDeliveryPathParser,
            IDeliveratorClient deliveratorClient,
            IOptions<ProxySettings> proxySettings) : base(logger, assetTracker, assetDeliveryPathParser)
        {
            this.deliveratorClient = deliveratorClient;
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
            var (assetRequest, statusCode) = await TryGetAssetDeliveryRequest<TimeBasedAssetDeliveryRequest>(httpContext);
            if (statusCode.HasValue || assetRequest == null)
            {
                return new StatusCodeResult(statusCode ?? HttpStatusCode.InternalServerError);
            }

            // If "HEAD" then add CORS - is this required here?
            var asset = await GetAsset(assetRequest);
            if (asset == null)
            {
                Logger.LogDebug("Request for {Path} asset not found", httpContext.Request.Path);
                return new StatusCodeResult(HttpStatusCode.NotFound);
            }
            
            var s3Path =
                $"{proxySettings.S3HttpBase}/{proxySettings.StorageBucket}/{assetRequest.GetAssetId()}{assetRequest.TimeBasedRequest}";
            if (!asset.RequiresAuth)
            {
                Logger.LogDebug("No auth for {Path}, 302 to S3 object {S3}", httpContext.Request.Path, s3Path);
                return new StatusCodeResult(HttpStatusCode.Redirect).WithHeader("Location", s3Path);
            }

            if (!await IsAuthenticated(assetRequest.GetAssetId(), httpContext))
            {
                Logger.LogDebug("User not authenticated for {Path}", httpContext.Request.Path);
                return new StatusCodeResult(HttpStatusCode.Unauthorized);
            }

            if (httpContext.Request.Method == "HEAD")
            {
                // quit with success as we've done all we need to
                return new StatusCodeResult(HttpStatusCode.OK);
            }
            
            return new ProxyActionResult(ProxyDestination.S3, s3Path);
        }

        private async Task<bool> IsAuthenticated(AssetId assetId, HttpContext httpContext)
        {
            var requestAuthentication = GetAuthMechanism(assetId, httpContext);
            if (!requestAuthentication.HasAuth) return false;

            return requestAuthentication.HaveCookie
                ? await deliveratorClient.VerifyCookieAuth(assetId, httpContext.Request, requestAuthentication.CookieName, requestAuthentication.CookieValue)
                : await deliveratorClient.VerifyBearerAuth(assetId, requestAuthentication.BearerToken!);
        }
        
        private RequestAuth GetAuthMechanism(AssetId assetId, HttpContext httpContext)
        {
            var cookieName = $"dlcs-token-{assetId.Customer}";
            if (httpContext.Request.Cookies.TryGetValue(cookieName, out var cookieValue))
            {
                Logger.LogDebug("Found cookie: '{CookieName}' for '{ImageId}'", assetId, cookieName);
                return RequestAuth.WithCookie(cookieName, cookieValue!);
            }
            
            var headerValue = httpContext.Request.GetAuthHeaderValue(AuthenticationHeaderUtils.BearerTokenScheme);
            if (headerValue != null)
            {
                Logger.LogDebug("Found bearer token for '{ImageId}'", assetId);
                return RequestAuth.WithBearerToken(headerValue.Parameter);
            }

            Logger.LogDebug("No auth found for '{ImageId}'", assetId);
            return new();
        }
    }
    
    internal class RequestAuth
    {
        public string? BearerToken { get; private init;}
        public bool HaveBearerToken { get; private init; }
        public bool HaveCookie { get; private init; }
        public string CookieValue { get; private init; }
        public string CookieName { get; private init; }
            
        public bool HasAuth => HaveBearerToken || HaveCookie;

        public static RequestAuth WithBearerToken(string bearerToken)
            => new() { BearerToken = bearerToken, HaveBearerToken = true };

        public static RequestAuth WithCookie(string cookieName, string cookieValue)
            => new() { HaveCookie = true, CookieName = cookieName, CookieValue = cookieValue };
    }
}