using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orchestrator.ReverseProxy;

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
        private readonly AssetDeliveryPathParser assetDeliveryPathParser;

        public ImageRequestHandler(
            ILogger<ImageRequestHandler> logger,
            IAssetRepository assetRepository,
            IThumbRepository thumbnailRepository,
            AssetDeliveryPathParser assetDeliveryPathParser)
        {
            this.logger = logger;
            this.assetRepository = assetRepository;
            this.thumbnailRepository = thumbnailRepository;
            this.assetDeliveryPathParser = assetDeliveryPathParser;
        }

        public async Task<ProxyAction> HandleRequest(HttpContext httpContext)
        {
            logger.LogDebug("Handling request for {Path}", httpContext.Request.Path);
            
            var assetRequest = await assetDeliveryPathParser.Parse(httpContext.Request.Path);
            
            // If "HEAD" then add CORS - is this required here?

            var asset = await GetAsset(assetRequest);
            if (DoesAssetRequireAuth(asset))
            {
                logger.LogDebug("Request for {Path} requires auth, proxying to orchestrator", httpContext.Request.Path);
                return new ProxyAction(ProxyTo.Orchestrator);
            }
            
            if (IsRequestForUVThumb(httpContext, assetRequest))
            {
                logger.LogDebug("Request for {Path} looks like UV thumb, proxying to thumbs", httpContext.Request.Path);
                return new ProxyAction(ProxyTo.Thumbs, GetUVThumbReplacementPath(assetRequest));
            }

            if (assetRequest.IIIFImageRequest.Region.Full && !assetRequest.IIIFImageRequest.Size.Max)
            {
                if (await IsRequestForKnownThumbSize(assetRequest))
                {
                    logger.LogDebug("'{Path}' can be handled by thumb, proxying to thumbs", httpContext.Request.Path);
                    return new ProxyAction(ProxyTo.Thumbs);
                }
            }
            
            return new ProxyAction(ProxyTo.CachingProxy);
        }

        private async Task<Asset> GetAsset(AssetDeliveryRequest assetRequest)
        {
            var imageId = assetRequest.GetAssetImageId();
            var asset = await assetRepository.GetAsset(imageId.ToString());
            return asset;
        }

        private bool DoesAssetRequireAuth(Asset asset) => !string.IsNullOrWhiteSpace(asset.Roles);
        
        // TODO have a flag to enable/disable this logic via config
        private bool IsRequestForUVThumb(HttpContext httpContext, AssetDeliveryRequest requestModel)
            => requestModel.IIIFImageRequest.ImageRequestPath == "/full/90,/0/default.jpg" && httpContext.Request.QueryString.Value.Contains("t=");
        
        // TODO pull size and /thumbs/ slug from config
        private string GetUVThumbReplacementPath(AssetDeliveryRequest requestModel)
            => $"thumbs/{requestModel.GetAssetImageId()}/full/!200,200/0/default.jpg";

        private async Task<bool> IsRequestForKnownThumbSize(AssetDeliveryRequest requestModel)
        {
            // NOTE - would this be quicker, since we have Asset, to calculate sizes? Would need Policy
            var candidate = await thumbnailRepository.GetThumbnailSizeCandidate(requestModel.Customer.Id,
                requestModel.Space, requestModel.IIIFImageRequest);
            return candidate.KnownSize;
        }
    }
}