using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.ReverseProxy;
using Orchestrator.Settings;

namespace Orchestrator.Images
{
    /// <summary>
    /// Reverse-proxy routing logic for /iiif-img/ requests 
    /// </summary>
    public class ImageRequestHandler
    {
        private readonly ILogger<ImageRequestHandler> logger;
        private readonly IAssetRepository assetRepository;
        private readonly IThumbRepository thumbnailRepository;
        private readonly IAssetDeliveryPathParser assetDeliveryPathParser;
        private readonly ProxySettings proxySettings;

        public ImageRequestHandler(
            ILogger<ImageRequestHandler> logger,
            IAssetRepository assetRepository,
            IThumbRepository thumbnailRepository,
            IAssetDeliveryPathParser assetDeliveryPathParser,
            IOptions<ProxySettings> proxySettings)
        {
            this.logger = logger;
            this.assetRepository = assetRepository;
            this.thumbnailRepository = thumbnailRepository;
            this.assetDeliveryPathParser = assetDeliveryPathParser;
            this.proxySettings = proxySettings.Value;
        }

        /// <summary>
        /// Handle /iiif-img/ request, returning object detailing operation that should be carried out.
        /// </summary>
        /// <param name="httpContext">Incoming <see cref="HttpContext"/> object</param>
        /// <returns><see cref="IProxyActionResult"/> object containing downstream target</returns>
        public async Task<IProxyActionResult> HandleRequest(HttpContext httpContext)
        {
            logger.LogDebug("Handling request for {Path}", httpContext.Request.Path);

            var (assetRequest, statusCode) = await TryGetAssetDeliveryRequest(httpContext);
            if (statusCode.HasValue || assetRequest == null)
            {
                return new StatusCodeProxyResult(statusCode ?? HttpStatusCode.InternalServerError);
            }

            // If "HEAD" then add CORS - is this required here?
            var asset = await GetAsset(assetRequest);
            if (DoesAssetRequireAuth(asset))
            {
                logger.LogDebug("Request for {Path} requires auth, proxying to orchestrator", httpContext.Request.Path);
                return new ProxyActionResult(ProxyTo.Orchestrator);
            }
            
            if (IsRequestForUVThumb(httpContext, assetRequest))
            {
                logger.LogDebug("Request for {Path} looks like UV thumb, proxying to thumbs", httpContext.Request.Path);
                return new ProxyActionResult(ProxyTo.Thumbs, GetUVThumbReplacementPath(assetRequest));
            }

            if (assetRequest.IIIFImageRequest.Region.Full && !assetRequest.IIIFImageRequest.Size.Max)
            {
                if (await IsRequestForKnownThumbSize(assetRequest))
                {
                    logger.LogDebug("'{Path}' can be handled by thumb, proxying to thumbs", httpContext.Request.Path);
                    return new ProxyActionResult(ProxyTo.Thumbs,
                        httpContext.Request.Path.ToString().Replace("iiif-img", "thumbs"));
                }
            }
            
            return new ProxyActionResult(ProxyTo.CachingProxy);
        }

        private async Task<(AssetDeliveryRequest? assetRequest, HttpStatusCode? statusCode)> TryGetAssetDeliveryRequest(
            HttpContext httpContext)
        {
            try
            {
                var assetRequest = await assetDeliveryPathParser.Parse(httpContext.Request.Path);
                return (assetRequest, null);
            }
            catch (KeyNotFoundException ex)
            {
                logger.LogError(ex, "Could not find Customer/Space from '{Path}'", httpContext.Request.Path);
                return (null, HttpStatusCode.NotFound);
            }
            catch (FormatException ex)
            {
                logger.LogError(ex, "Error parsing path '{Path}'", httpContext.Request.Path);
                return (null, HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                // TODO - is this the correct status?
                logger.LogError(ex, "Error parsing path '{Path}'", httpContext.Request.Path);
                return (null, HttpStatusCode.InternalServerError);
            }
        }

        private async Task<Asset> GetAsset(AssetDeliveryRequest assetRequest)
        {
            var imageId = assetRequest.GetAssetImageId();
            var asset = await assetRepository.GetAsset(imageId.ToString());
            return asset;
        }

        private bool DoesAssetRequireAuth(Asset asset) => !string.IsNullOrWhiteSpace(asset.Roles);
        
        private bool IsRequestForUVThumb(HttpContext httpContext, AssetDeliveryRequest requestModel)
            => proxySettings.CheckUVThumbs &&
               requestModel.IIIFImageRequest.ImageRequestPath == "/full/90,/0/default.jpg" &&
               httpContext.Request.QueryString.Value.Contains("t=");
        
        private string GetUVThumbReplacementPath(AssetDeliveryRequest requestModel) => 
            $"{proxySettings.ThumbsPath}/{requestModel.GetAssetImageId()}/full/{proxySettings.UVThumbReplacementPath}/0/default.jpg";

        // TODO handle resizing via config. Optionally with path regex (resize X but not Y)
        private async Task<bool> IsRequestForKnownThumbSize(AssetDeliveryRequest requestModel)
        {
            // NOTE - would this be quicker, since we have Asset, to calculate sizes? Would need Policy
            var candidate = await thumbnailRepository.GetThumbnailSizeCandidate(requestModel.Customer.Id,
                requestModel.Space, requestModel.IIIFImageRequest);
            return candidate.KnownSize;
        }
    }
}