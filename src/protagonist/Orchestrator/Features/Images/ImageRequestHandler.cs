using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Repository.Assets;
using DLCS.Web.IIIF;
using DLCS.Web.Requests.AssetDelivery;
using IIIF;
using IIIF.ImageApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images;

/// <summary>
/// Reverse-proxy routing logic for /iiif-img/ requests 
/// </summary>
public class ImageRequestHandler
{
    private readonly ILogger<ImageRequestHandler> logger;
    private readonly AssetRequestProcessor assetRequestProcessor;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ICustomHeaderRepository customHeaderRepository;
    private readonly IOptions<OrchestratorSettings> orchestratorSettings;
    private readonly Dictionary<string, CompiledRegexThumbUpscaleConfig> upscaleConfig;
    private readonly bool haveUpscaleRules;

    public ImageRequestHandler(
        ILogger<ImageRequestHandler> logger,
        AssetRequestProcessor assetRequestProcessor,
        IServiceScopeFactory scopeFactory,
        ICustomHeaderRepository customHeaderRepository,
        IOptions<OrchestratorSettings> orchestratorSettings)
    {
        this.logger = logger;
        this.assetRequestProcessor = assetRequestProcessor;
        this.scopeFactory = scopeFactory;
        this.customHeaderRepository = customHeaderRepository;
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
        var (assetRequest, statusCode) =
            await assetRequestProcessor.TryGetAssetDeliveryRequest<ImageAssetDeliveryRequest>(httpContext);
        if (statusCode.HasValue || assetRequest == null)
        {
            return new StatusCodeResult(statusCode ?? HttpStatusCode.InternalServerError);
        }
        
        // If "HEAD" then add CORS - is this required here?
        var orchestrationAsset = await assetRequestProcessor.GetAsset(assetRequest);
        if (orchestrationAsset is not OrchestrationImage orchestrationImage)
        {
            logger.LogDebug("Request for {Path} asset not found", httpContext.Request.Path);
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        if (orchestrationImage.RequiresAuth)
        { 
            if (await IsRequestUnauthorised(assetRequest, orchestrationImage))
            {
                return new StatusCodeResult(HttpStatusCode.Unauthorized);
            }
        }
        
        /*
        Is known thumb size
        Is full and smaller than biggest thumb size
        is full and bigger than biggest thumb size
        anything else

        1 - proxy thumb
        2 - resample next thumb size up
        3 - off to S3-cantaloupe
        4 - off to filesystem cantaloupe (including orchestration if required)
        for 2 and 3 - if the asked-for thumb is not on S3 but is in the thumbnail policy list, save it to S3 on the way out
        ... and use the No 3 S3 cantaloupe, not the orchestrating path
         */
        if (RegionFullNotMax(assetRequest))
        {
            var canHandleByThumbResponse = CanRequestBeHandledByThumb(assetRequest, orchestrationImage);
            if (canHandleByThumbResponse.CanHandle)
            {
                logger.LogDebug("'{Path}' can be handled by thumb, proxying to thumbs. IsResize: {IsResize}",
                    httpContext.Request.Path, canHandleByThumbResponse.IsResize);
                
                var pathReplacement = canHandleByThumbResponse.IsResize
                    ? orchestratorSettings.Value.Proxy.ThumbResizePath
                    : orchestratorSettings.Value.Proxy.ThumbsPath;
                var proxyDestination = canHandleByThumbResponse.IsResize
                    ? ProxyDestination.ResizeThumbs
                    : ProxyDestination.Thumbs;
                var proxyResult = new ProxyActionResult(proxyDestination,
                    orchestrationImage.RequiresAuth,
                    httpContext.Request.Path.ToString().Replace("iiif-img", pathReplacement));
                await SetCustomHeaders(orchestrationImage, proxyResult);
                return proxyResult;
            }
        }

        return await GenerateImageResult(orchestrationImage, assetRequest);
    }
    
    private static bool RegionFullNotMax(ImageAssetDeliveryRequest assetRequest) 
        => assetRequest.IIIFImageRequest.Region.Full && !assetRequest.IIIFImageRequest.Size.Max;

    private async Task<bool> IsRequestUnauthorised(ImageAssetDeliveryRequest assetRequest,
        OrchestrationImage orchestrationImage)
    {
        // If the image has a maxUnauthorised, and the region is /full/ then user may be able to see requested
        // size without doing auth check
        var imageRequest = assetRequest.IIIFImageRequest;
        if (imageRequest.Region.Full && orchestrationImage.MaxUnauthorised > 0)
        {
            var imageSize = new Size(orchestrationImage.Width, orchestrationImage.Height);
            var proposedSize = imageRequest.Size.GetResultingSize(imageSize);
            
            // If resulting maxDimension < maxUnauthorised then anyone can view
            if (proposedSize.MaxDimension <= orchestrationImage.MaxUnauthorised)
            {
                logger.LogDebug(
                    "Request for {ImageRequest} requires auth but viewable due to maxUnauthorised size of {MaxUnauth}",
                    imageRequest.OriginalPath, orchestrationImage.MaxUnauthorised);
                return false;
            }
        }

        // IAssetAccessValidator is in container with a Lifetime.Scope
        using var scope = scopeFactory.CreateScope();
        var assetAccessValidator = scope.ServiceProvider.GetRequiredService<IAssetAccessValidator>();
        var authResult =
            await assetAccessValidator.TryValidate(assetRequest.Customer.Id,
                orchestrationImage.Roles, AuthMechanism.Cookie);

        return authResult == AssetAccessResult.Unauthorized;
    }

    // TODO handle known thumb size that doesn't exist yet - call image-server and save to s3 on way back
    private (bool CanHandle, bool IsResize) CanRequestBeHandledByThumb(ImageAssetDeliveryRequest requestModel,
        OrchestrationImage orchestrationImage)
    {
        var imageRequest = requestModel.IIIFImageRequest;
        // Contains Image Request Parameters that thumbs can't handle, abort
        if (!imageRequest.IsCandidateForThumbHandling(out _)) return (false, false);

        var openSizes = orchestrationImage.OpenThumbs.Select(wh => Size.FromArray(wh)).ToList();

        // No open thumbs so cannot handle by thumb, abort
        if (openSizes.IsNullOrEmpty()) return (false, false);

        // Check if settings.ThumbnailResizeConfig contains values, if not then as-is
        var canResizeThumbs = orchestratorSettings.Value.Proxy.CanResizeThumbs;
        var candidate = ThumbnailCalculator.GetCandidate(openSizes, imageRequest, canResizeThumbs);

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
                logger.LogDebug("ThumbUpscaleConfig {ResizeKey} matches Asset {Asset}", key, assetId);
                var diff = Size.GetSizeIncreasePercent(resizeCandidate.Ideal, resizeCandidate.SmallerSize);
                if (diff <= config.UpscaleThreshold)
                {
                    return (true, true);
                }
            }
        }

        return (false, false);
    }

    private async Task<IProxyActionResult> GenerateImageResult(OrchestrationImage orchestrationImage,
        ImageAssetDeliveryRequest requestModel)
    {
        string? targetPath = GetImageServerPath(orchestrationImage, requestModel);
        if (string.IsNullOrEmpty(targetPath))
        {
            logger.LogDebug("Unable to fulfil image request: {Path}. ImageServer path found",
                requestModel.NormalisedFullPath);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }

        var imageServerPath = $"{targetPath}{requestModel.IIIFImageRequest.ImageRequestPath}";
        var proxyImageServerResult = new ProxyImageServerResult(orchestrationImage, orchestrationImage.RequiresAuth,
            ProxyDestination.ImageServer, imageServerPath);
        await SetCustomHeaders(orchestrationImage, proxyImageServerResult);
        return proxyImageServerResult;
    }

    private string? GetImageServerPath(OrchestrationImage orchestrationImage, ImageAssetDeliveryRequest requestModel)
    {
        var settings = orchestratorSettings.Value;
        if (requestModel.VersionPathValue.HasText())
        {
            var imageApiVersion = requestModel.VersionPathValue.ParseToIIIFImageApiVersion();
            if (!imageApiVersion.HasValue) return null;

            return settings.GetImageServerPath(orchestrationImage.AssetId, imageApiVersion.Value);
        }
        else
        {
            return settings.GetImageServerPath(orchestrationImage.AssetId, settings.DefaultIIIFImageVersion);
        }
    }

    private async Task SetCustomHeaders(OrchestrationImage orchestrationImage, 
        ProxyActionResult proxyImageServerResult)
    {
        // order of precedence (low -> high), same header will be overwritten if present
        var customerHeaders = (await customHeaderRepository.GetForCustomer(orchestrationImage.AssetId.Customer))
            .ToList();

        CustomHeaderProcessor.SetProxyImageHeaders(customerHeaders, orchestrationImage, proxyImageServerResult);
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