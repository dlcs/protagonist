using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Repository.Assets;
using DLCS.Web.Requests.AssetDelivery;
using IIIF;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.ReverseProxy;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images
{
    /// <summary>
    /// Reverse-proxy routing logic for /iiif-img/ requests 
    /// </summary>
    public class ImageRequestHandler : RequestHandlerBase
    {
        private readonly ProxySettings proxySettings;

        public ImageRequestHandler(
            ILogger<ImageRequestHandler> logger,
            IAssetTracker assetTracker,
            IAssetDeliveryPathParser assetDeliveryPathParser,
            IOptions<ProxySettings> proxySettings) : base(logger, assetTracker, assetDeliveryPathParser)
        {
            this.proxySettings = proxySettings.Value;
        }

        /// <summary>
        /// Handle /iiif-img/ request, returning object detailing operation that should be carried out.
        /// </summary>
        /// <param name="httpContext">Incoming <see cref="HttpContext"/> object</param>
        /// <returns><see cref="IProxyActionResult"/> object containing downstream target</returns>
        public async Task<IProxyActionResult> HandleRequest(HttpContext httpContext)
        {
            var (assetRequest, statusCode) = await TryGetAssetDeliveryRequest<ImageAssetDeliveryRequest>(httpContext);
            if (statusCode.HasValue || assetRequest == null)
            {
                return new StatusCodeProxyResult(statusCode ?? HttpStatusCode.InternalServerError);
            }

            // If "HEAD" then add CORS - is this required here?
            var orchestrationAsset = await GetAsset(assetRequest);
            if (orchestrationAsset is not OrchestrationImage orchestrationImage)
            {
                Logger.LogDebug("Request for {Path} asset not found", httpContext.Request.Path);
                return new StatusCodeProxyResult(HttpStatusCode.NotFound);
            }
            
            if (orchestrationImage.RequiresAuth)
            {
                Logger.LogDebug("Request for {Path} requires auth, proxying to orchestrator", httpContext.Request.Path);
                return new ProxyActionResult(ProxyDestination.Orchestrator, assetRequest.NormalisedFullPath);
            }
            
            if (IsRequestForUVThumb(httpContext, assetRequest))
            {
                Logger.LogDebug("Request for {Path} looks like UV thumb, proxying to thumbs", httpContext.Request.Path);
                return new ProxyActionResult(ProxyDestination.Thumbs, GetUVThumbReplacementPath(orchestrationImage.AssetId));
            }

            if (assetRequest.IIIFImageRequest.Region.Full && !assetRequest.IIIFImageRequest.Size.Max)
            {
                if (IsRequestForKnownThumbSize(assetRequest, orchestrationImage))
                {
                    Logger.LogDebug("'{Path}' can be handled by thumb, proxying to thumbs", httpContext.Request.Path);
                    return new ProxyActionResult(ProxyDestination.Thumbs,
                        httpContext.Request.Path.ToString().Replace("iiif-img", "thumbs"));
                }
            }
            
            return new ProxyActionResult(ProxyDestination.CachingProxy, assetRequest.NormalisedFullPath);
        }

        private bool IsRequestForUVThumb(HttpContext httpContext, ImageAssetDeliveryRequest requestModel)
            => proxySettings.CheckUVThumbs &&
               requestModel.IIIFImageRequest.ImageRequestPath == "/full/90,/0/default.jpg" &&
               httpContext.Request.QueryString.Value.Contains("t=");
        
        private string GetUVThumbReplacementPath(AssetId assetId) => 
            $"{proxySettings.ThumbsPath}/{assetId}/full/{proxySettings.UVThumbReplacementPath}/0/default.jpg";

        // TODO handle resizing via config. Optionally with path regex (resize X but not Y)
        // TODO thumb-size lookup could be cached
        private bool IsRequestForKnownThumbSize(ImageAssetDeliveryRequest requestModel, OrchestrationImage orchestrationImage)
        {
            var openSizes = orchestrationImage.OpenThumbs.Select(wh => Size.FromArray(wh)).ToList();
            var candidate = ThumbnailCalculator.GetCandidate(openSizes, requestModel.IIIFImageRequest, false);
            return candidate.KnownSize;
        }
    }
}