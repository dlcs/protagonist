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
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images
{
    /// <summary>
    /// Reverse-proxy routing logic for /iiif-img/ requests 
    /// </summary>
    public class ImageRequestHandler : RequestHandlerBase
    {
        private readonly IOptions<OrchestratorSettings> orchestratorSettings;

        public ImageRequestHandler(
            ILogger<ImageRequestHandler> logger,
            IAssetTracker assetTracker,
            IAssetDeliveryPathParser assetDeliveryPathParser,
            IOptions<OrchestratorSettings> orchestratorSettings) : base(logger, assetTracker, assetDeliveryPathParser)
        {
            this.orchestratorSettings = orchestratorSettings;
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
                return new StatusCodeResult(statusCode ?? HttpStatusCode.InternalServerError);
            }

            // If "HEAD" then add CORS - is this required here?
            var orchestrationAsset = await GetAsset(assetRequest);
            if (orchestrationAsset is not OrchestrationImage orchestrationImage)
            {
                Logger.LogDebug("Request for {Path} asset not found", httpContext.Request.Path);
                return new StatusCodeResult(HttpStatusCode.NotFound);
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
            
            /*
            Is known thumb size
            Is full and smaller than biggest thumb size
            is full and bigger than biggest thumb size
            anything else

            1 - proxy thumb
            2 - resample next thumb size up
            3 - off to S3-cantaloupe
            4 - off to filesystem cantaloupe (including orchestration if reqd)
            for 2 and 3 - if the asked-for thumb is not on S3 but is in the thumbnail policy list, save it to S3 on the way out
            ... and use the No 3 S3 cantaloupe, not the orchestrating path
             */
            if (assetRequest.IIIFImageRequest.Region.Full && !assetRequest.IIIFImageRequest.Size.Max)
            {
                if (IsRequestForKnownThumbSize(assetRequest, orchestrationImage))
                {
                    Logger.LogDebug("'{Path}' can be handled by thumb, proxying to thumbs", httpContext.Request.Path);
                    return new ProxyActionResult(ProxyDestination.Thumbs,
                        httpContext.Request.Path.ToString().Replace("iiif-img", "thumbs"));
                }
            }

            return GenerateImageResult(orchestrationImage, assetRequest);
        }

        private bool IsRequestForUVThumb(HttpContext httpContext, ImageAssetDeliveryRequest requestModel)
            => orchestratorSettings.Value.Proxy.CheckUVThumbs &&
               requestModel.IIIFImageRequest.ImageRequestPath == "/full/90,/0/default.jpg" &&
               httpContext.Request.QueryString.Value.Contains("t=");

        private string GetUVThumbReplacementPath(AssetId assetId) =>
            $"{orchestratorSettings.Value.Proxy.ThumbsPath}/{assetId}/full/{orchestratorSettings.Value.Proxy.UVThumbReplacementPath}/0/default.jpg";

        // TODO handle resizing via config. Optionally with path regex (resize X but not Y)
        // TODO handle known thumb size that doesn't exist yet - call image-server and save to s3 on way back
        private bool IsRequestForKnownThumbSize(ImageAssetDeliveryRequest requestModel, OrchestrationImage orchestrationImage)
        {
            var openSizes = orchestrationImage.OpenThumbs.Select(wh => Size.FromArray(wh)).ToList();
            var candidate = ThumbnailCalculator.GetCandidate(openSizes, requestModel.IIIFImageRequest, false);
            return candidate.KnownSize;
        }

        private ProxyImageServerResult GenerateImageResult(OrchestrationImage orchestrationImage,
            ImageAssetDeliveryRequest requestModel)
        {
            // NOTE - this is for IIP image only
            var targetPath = orchestratorSettings.Value.GetImageLocalPath(orchestrationImage.AssetId, true);
            var root = orchestratorSettings.Value.Proxy.ImageServerRoot;
            var imageServerPath =
                $"{root}/fcgi-bin/iipsrv.fcgi?IIIF={targetPath}{requestModel.IIIFImageRequest.ImageRequestPath}";
            return new ProxyImageServerResult(orchestrationImage, ProxyDestination.ImageServer,
                imageServerPath);
        }
    }
}