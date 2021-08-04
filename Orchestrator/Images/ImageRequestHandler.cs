using System.Net;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.ReverseProxy;
using Orchestrator.Settings;

namespace Orchestrator.Images
{
    /// <summary>
    /// Reverse-proxy routing logic for /iiif-img/ requests 
    /// </summary>
    public class ImageRequestHandler : RequestHandlerBase
    {
        private readonly IThumbRepository thumbnailRepository;
        private readonly ProxySettings proxySettings;

        public ImageRequestHandler(
            ILogger<ImageRequestHandler> logger,
            IAssetTracker assetTracker,
            IThumbRepository thumbnailRepository,
            IAssetDeliveryPathParser assetDeliveryPathParser,
            IOptions<ProxySettings> proxySettings) : base(logger, assetTracker, assetDeliveryPathParser)
        {
            this.thumbnailRepository = thumbnailRepository;
            this.proxySettings = proxySettings.Value;
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
            if (asset.RequiresAuth)
            {
                Logger.LogDebug("Request for {Path} requires auth, proxying to orchestrator", httpContext.Request.Path);
                return new ProxyActionResult(ProxyDestination.Orchestrator);
            }
            
            if (IsRequestForUVThumb(httpContext, assetRequest))
            {
                Logger.LogDebug("Request for {Path} looks like UV thumb, proxying to thumbs", httpContext.Request.Path);
                return new ProxyActionResult(ProxyDestination.Thumbs, GetUVThumbReplacementPath(asset.AssetId));
            }

            if (assetRequest.IIIFImageRequest.Region.Full && !assetRequest.IIIFImageRequest.Size.Max)
            {
                if (await IsRequestForKnownThumbSize(assetRequest))
                {
                    Logger.LogDebug("'{Path}' can be handled by thumb, proxying to thumbs", httpContext.Request.Path);
                    return new ProxyActionResult(ProxyDestination.Thumbs,
                        httpContext.Request.Path.ToString().Replace("iiif-img", "thumbs"));
                }
            }
            
            return new ProxyActionResult(ProxyDestination.CachingProxy);
        }

        private bool IsRequestForUVThumb(HttpContext httpContext, ImageAssetDeliveryRequest requestModel)
            => proxySettings.CheckUVThumbs &&
               requestModel.IIIFImageRequest.ImageRequestPath == "/full/90,/0/default.jpg" &&
               httpContext.Request.QueryString.Value.Contains("t=");
        
        private string GetUVThumbReplacementPath(string assetId) => 
            $"{proxySettings.ThumbsPath}/{assetId}/full/{proxySettings.UVThumbReplacementPath}/0/default.jpg";

        // TODO handle resizing via config. Optionally with path regex (resize X but not Y)
        // TODO thumb-size lookup could be cached
        private async Task<bool> IsRequestForKnownThumbSize(ImageAssetDeliveryRequest requestModel)
        {
            // NOTE - would this be quicker, since we have Asset, to calculate sizes? Would need Policy
            var candidate = await thumbnailRepository.GetThumbnailSizeCandidate(requestModel.Customer.Id,
                requestModel.Space, requestModel.IIIFImageRequest);
            return candidate.KnownSize;
        }
    }
}