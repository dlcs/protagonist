using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
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
        private readonly Dictionary<string, CompiledRegexThumbUpscaleConfig> upscaleConfig;
        private readonly bool haveUpscaleRules;

        public ImageRequestHandler(
            ILogger<ImageRequestHandler> logger,
            IAssetTracker assetTracker,
            IAssetDeliveryPathParser assetDeliveryPathParser,
            IOptions<OrchestratorSettings> orchestratorSettings) : base(logger, assetTracker, assetDeliveryPathParser)
        {
            this.orchestratorSettings = orchestratorSettings;

            upscaleConfig = orchestratorSettings.Value.Proxy?.ThumbUpscaleConfig?
                                .Where(kvp => kvp.Value.UpscaleThreshold > 0)
                                .ToDictionary(kvp => kvp.Key, kvp => new CompiledRegexThumbUpscaleConfig(kvp.Value)) ??
                            new Dictionary<string, CompiledRegexThumbUpscaleConfig>();
            haveUpscaleRules = upscaleConfig.Count > 0;
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
                var canHandleByThumbResponse = CanRequestBeHandledByThumb(assetRequest, orchestrationImage);
                if (canHandleByThumbResponse.CanHandle)
                {
                    Logger.LogDebug("'{Path}' can be handled by thumb, proxying to thumbs. IsResize: {IsResize}",
                        httpContext.Request.Path, canHandleByThumbResponse.IsResize);
                    
                    var pathReplacement = canHandleByThumbResponse.IsResize
                        ? orchestratorSettings.Value.Proxy.ThumbResizePath
                        : orchestratorSettings.Value.Proxy.ThumbsPath;
                    var proxyDestination = canHandleByThumbResponse.IsResize
                        ? ProxyDestination.ResizeThumbs
                        : ProxyDestination.Thumbs;
                    return new ProxyActionResult(proxyDestination,
                        httpContext.Request.Path.ToString().Replace("iiif-img", pathReplacement));
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

        // TODO handle known thumb size that doesn't exist yet - call image-server and save to s3 on way back
        private (bool CanHandle, bool IsResize) CanRequestBeHandledByThumb(ImageAssetDeliveryRequest requestModel, OrchestrationImage orchestrationImage)
        {
            // TODO - must be for a jpg
            var openSizes = orchestrationImage.OpenThumbs.Select(wh => Size.FromArray(wh)).ToList();
            
            // No open thumbs so cannot handle by thumb, abort
            if (openSizes.IsNullOrEmpty()) return (false, false);
            
            // Check if settings.ThumbnailResizeConfig contains values, if not then as-is
            var canResizeThumbs = orchestratorSettings.Value.Proxy.CanResizeThumbs;
            var candidate = ThumbnailCalculator.GetCandidate(openSizes, requestModel.IIIFImageRequest, canResizeThumbs);
            
            // Exact match - can handle
            if (candidate.KnownSize) return (true, false);

            // Resizing not supported, abort
            if (!canResizeThumbs || candidate is not ResizableSize resizeCandidate) return (false, false);

            // There's a larger size - this can be used to resize
            if (resizeCandidate.LargerSize != null) return (true, true);

            // There are no upscale rules OR no smaller sizes to upscale so abort
            if (!haveUpscaleRules || resizeCandidate.SmallerSize == null) return (false, false); 

            // If here there are smaller sizes and upscaling is supported, check to see if there are any matches 
            var assetId = orchestrationImage.AssetId.ToString();
            foreach (var (key, config) in upscaleConfig)
            {
                if (config.CompiledAssetRegex.IsMatch(assetId))
                {
                    Logger.LogDebug("ThumbUpscaleConfig {ResizeKey} matches Asset {Asset}", key, assetId);
                    var diff = Size.GetSizeIncreasePercent(resizeCandidate.Ideal, resizeCandidate.SmallerSize);
                    if (diff <= config.UpscaleThreshold)
                    {
                        return (true, true);
                    }
                }
            }

            return (false, false);
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

    /// <summary>
    /// This is called a lot so compiled regex for fast performance
    /// </summary>
    internal class CompiledRegexThumbUpscaleConfig : ThumbUpscaleConfig
    {
        public Regex CompiledAssetRegex { get; }

        public CompiledRegexThumbUpscaleConfig(ThumbUpscaleConfig source)
        {
            AssetIdRegex = source.AssetIdRegex;
            UpscaleThreshold = source.UpscaleThreshold;
            CompiledAssetRegex = new Regex(AssetIdRegex, RegexOptions.Compiled);
        }
    }
}