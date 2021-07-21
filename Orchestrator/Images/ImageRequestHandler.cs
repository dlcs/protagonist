using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.PathElements;
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
        private readonly IPathCustomerRepository pathCustomerRepository;
        private readonly IThumbRepository thumbnailRepository;

        public ImageRequestHandler(
            ILogger<ImageRequestHandler> logger,
            IAssetRepository assetRepository,
            IPathCustomerRepository pathCustomerRepository,
            IThumbRepository thumbnailRepository)
        {
            this.logger = logger;
            this.assetRepository = assetRepository;
            this.pathCustomerRepository = pathCustomerRepository;
            this.thumbnailRepository = thumbnailRepository;
        }

        public async Task<ProxyAction> HandleRequest(HttpContext httpContext)
        {
            logger.LogDebug("Handling request for {Path}", httpContext.Request.Path);
            
            var requestModel = new ImageRequestModel(httpContext);
            
            // If "HEAD" then add CORS - is this required here?
            
            // Call /requiresAuth/
            var asset = await GetAsset(requestModel);
            if (DoesAssetRequireAuth(asset))
            {
                logger.LogDebug("Request for {Path} requires auth, proxying to orchestrator", httpContext.Request.Path);
                return new ProxyAction(ProxyTo.Orchestrator);
            }
            
            if (IsRequestForUVThumb(httpContext, requestModel))
            {
                logger.LogDebug("Request for {Path} looks like UV thumb, proxying to thumbs", httpContext.Request.Path);
                return new ProxyAction(ProxyTo.Thumbs, GetUVThumbReplacementPath(requestModel));
            }

            var customerPathElement = await pathCustomerRepository.GetCustomer(requestModel.Customer);
            requestModel.SetCustomerPathElement(customerPathElement);

            if (requestModel.ImageRequest.Region.Full && !requestModel.ImageRequest.Size.Max)
            {
                if (await IsRequestForKnownThumbSize(requestModel))
                {
                    logger.LogDebug("'{Path}' can be handled by thumb, proxying to thumbs", httpContext.Request.Path);
                    return new ProxyAction(ProxyTo.Thumbs);
                }
            }
            
            return new ProxyAction(ProxyTo.CachingProxy);
        }

        private async Task<Asset> GetAsset(ImageRequestModel requestModel)
        {
            var imageId = requestModel.ToAssetImageId();
            var asset = await assetRepository.GetAsset(imageId.ToString());
            return asset;
        }

        private bool DoesAssetRequireAuth(Asset asset) => !string.IsNullOrWhiteSpace(asset.Roles);
        
        // TODO have a flag to enable/disable this logic via config
        private bool IsRequestForUVThumb(HttpContext httpContext, ImageRequestModel requestModel)
            => requestModel.ImageRequestPath == "full/90,/0/default.jpg" && httpContext.Request.QueryString.Value.Contains("t=");
        
        // TODO pull size and /thumbs/ slug from config
        private string GetUVThumbReplacementPath(ImageRequestModel requestModel)
            => $"/thumbs/{requestModel.ToAssetImageId()}/full/!200,200/0/default.jpg";

        private async Task<bool> IsRequestForKnownThumbSize(ImageRequestModel requestModel)
        {
            // NOTE - would this be quicker, since we have Asset, to calculate sizes? Would need Policy
            var candidate = await thumbnailRepository.GetThumbnailSizeCandidate(requestModel.CustomerId.Value,
                requestModel.SpaceId, requestModel.ImageRequest);
            return candidate.KnownSize;
        }
    }
}