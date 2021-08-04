using System.Net;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Web.Auth;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.ReverseProxy;
using Orchestrator.Settings;

namespace Orchestrator.AV
{
    /// <summary>
    /// Reverse-proxy routing logic for /iiif-av/ requests 
    /// </summary>
    public class AVRequestHandler : RequestHandlerBase
    {
        private readonly IDeliveratorClient deliveratorClient;
        private readonly ProxySettings proxySettings;

        public AVRequestHandler(
            ILogger<AVRequestHandler> logger,
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
            var (assetRequest, statusCode) = await TryGetAssetDeliveryRequest<ImageAssetDeliveryRequest>(httpContext);
            if (statusCode.HasValue || assetRequest == null)
            {
                return new StatusCodeProxyResult(statusCode ?? HttpStatusCode.InternalServerError);
            }

            // If "HEAD" then add CORS - is this required here?
            var asset = await GetAsset(assetRequest);
            var s3Path = $"{proxySettings.S3HttpBase}/{proxySettings.StorageBucket}/{assetRequest.GetAssetImageId()}";
            if (!asset.RequiresAuth)
            {
                Logger.LogDebug("No auth for {Path}, 302 to S3 object {S3}", httpContext.Request.Path, s3Path);
                return new StatusCodeProxyResult(HttpStatusCode.Redirect).WithHeader("Location", s3Path);
            }

            if (!await IsAuthenticated(assetRequest.GetAssetImageId(), httpContext))
            {
                Logger.LogDebug("User not authenticated for {Path}", httpContext.Request.Path);
                return new StatusCodeProxyResult(HttpStatusCode.Unauthorized);
            }

            if (httpContext.Request.Method == "HEAD")
            {
                // quit with success as we've done all we need to
                return new StatusCodeProxyResult(HttpStatusCode.OK);
            }
            
            return new ProxyActionResult(ProxyDestination.S3, s3Path);
        }

        private async Task<bool> IsAuthenticated(AssetId assetId, HttpContext httpContext)
        {
            var authStuff = GetAuthMechanism(assetId, httpContext);
            if (!authStuff.HasAuth)
            {
                return false;
            }

            return authStuff.HaveCookie
                ? await deliveratorClient.VerifyCookieAuth(assetId, httpContext.Request)
                : await deliveratorClient.VerifyBearerAuth(assetId, authStuff.BearerToken!);
        }
        
        private RequestAuth GetAuthMechanism(AssetId assetId, HttpContext httpContext)
        {
            var cookieName = $"dlcs-token-{assetId.Customer}";
            if (httpContext.Request.Cookies.ContainsKey(cookieName))
            {
                Logger.LogDebug("Found cookie: '{CookieName}' for '{ImageId}'", assetId,
                    cookieName);
                return RequestAuth.WithCookie();
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
            
        public bool HasAuth => HaveBearerToken || HaveCookie;

        public static RequestAuth WithBearerToken(string bearerToken)
            => new() {BearerToken = bearerToken, HaveBearerToken = true};

        public static RequestAuth WithCookie()
            => new() {HaveCookie = true};
    }
}