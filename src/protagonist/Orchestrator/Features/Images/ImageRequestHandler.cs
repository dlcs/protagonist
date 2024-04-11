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
        
        var orchestrationImage = await assetRequestProcessor.GetAsset<OrchestrationImage>(httpContext, assetRequest);
        if (orchestrationImage == null)
        {
            logger.LogDebug("Request for {Path}: asset not found", httpContext.Request.Path);
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }
        
        if (!orchestrationImage.Channels.HasFlag(AvailableDeliveryChannel.Image))
        {
            logger.LogDebug("Request for {Path}: asset not available in 'image' channel", httpContext.Request.Path);
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        if (orchestrationImage.IsNotFound())
        {
            logger.LogDebug("Request for {Path}: asset never been processed", httpContext.Request.Path);
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        var proxyActionResult = await HandleRequestInternal(httpContext, orchestrationImage, assetRequest);
        if (proxyActionResult is StatusCodeResult) return proxyActionResult;

        if (proxyActionResult is not ProxyActionResult result)
        {
            logger.LogError(
                "Proxy action result for {Path} isn't a StatusCodeResult or ProxyActionResult. It is: {ResultType}",
                httpContext.Request.Path, proxyActionResult.GetType());
            return new StatusCodeResult(HttpStatusCode.InternalServerError);
        }

        await SetCustomHeaders(orchestrationImage, result);
        return proxyActionResult;
    }

    private async Task<IProxyActionResult> HandleRequestInternal(HttpContext httpContext,
        OrchestrationImage orchestrationImage, ImageAssetDeliveryRequest assetRequest)
    {
        if (orchestrationImage.RequiresAuth)
        {
            if (await IsRequestUnauthorised(assetRequest, orchestrationImage))
            {
                return new StatusCodeResult(HttpStatusCode.Unauthorized);
            }
        }

        // /full/ request but not /full/max/ - can it be handled by thumbnail service?
        if (RegionFullNotMax(assetRequest, orchestrationImage))
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
                return proxyResult;
            }
        }

        // /full/ that cannot be handled by thumbs (e.g. format, size, rotation, quality), handle with special-server
        if (assetRequest.IIIFImageRequest.Region.Full)
        {
            if (orchestrationImage.S3Location.IsNullOrEmpty())
            {
                // Rare occurence - fall through to image server which will handle reingest request
                logger.LogInformation("{AssetId} candidate for SpecialServer handling but s3Location empty",
                    orchestrationImage.AssetId);
            }
            else
            {
                return GenerateImageServerProxyResult(orchestrationImage, assetRequest, specialServer: true);
            }
        }

        // Fallback to image-server, with orchestration if required
        return GenerateImageServerProxyResult(orchestrationImage, assetRequest, specialServer: false);
    }

    private static bool RegionFullNotMax(ImageAssetDeliveryRequest assetRequest, OrchestrationImage orchestrationImage) 
        => IsRequestFullOrEquivalent(assetRequest, orchestrationImage) && !assetRequest.IIIFImageRequest.Size.Max;

    private static bool IsRequestFullOrEquivalent(ImageAssetDeliveryRequest assetRequest,
        OrchestrationImage orchestrationImage)
    {
        if (assetRequest.IIIFImageRequest.Region.Full)
        {
            return true;
        }

        if (assetRequest.IIIFImageRequest.Region.Square &&
            orchestrationImage.Width == orchestrationImage.Height)
        {
            return true;
        }

        if (!assetRequest.IIIFImageRequest.Region.Percent &&
            assetRequest.IIIFImageRequest.Region.X + assetRequest.IIIFImageRequest.Region.Y == 0 &&
            orchestrationImage.Width == assetRequest.IIIFImageRequest.Region.W &&
            orchestrationImage.Height == assetRequest.IIIFImageRequest.Region.H)
        {
            return true;
        }

        return false;
    }
    
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
        var authResult = await assetAccessValidator.TryValidate(assetRequest.GetAssetId(), orchestrationImage.Roles,
            AuthMechanism.Cookie);

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

    private IProxyActionResult GenerateImageServerProxyResult(OrchestrationImage orchestrationImage,
        ImageAssetDeliveryRequest requestModel, bool specialServer)
    {
        var imageApiVersion = GetImageApiVersion(requestModel);
        if (imageApiVersion == null)
        {
            logger.LogDebug("Unable to fulfil image request: {Path}. Could not parse ImageVersion",
                requestModel.NormalisedFullPath);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }
        
        // get the redirect path - S3:// path for special-server or /path/on/disk for image-server
        var settings = orchestratorSettings.Value;
        var downstreamPath = specialServer
            ? settings.GetSpecialServerPath(orchestrationImage.S3Location, imageApiVersion.Value)
            : settings.GetImageServerPath(orchestrationImage.AssetId, imageApiVersion.Value);

        if (string.IsNullOrEmpty(downstreamPath))
        {
            logger.LogDebug("Unable to fulfil image request: {Path}. Could not generate ImageServer path",
                requestModel.NormalisedFullPath);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }
        
        var imageServerPath = $"{downstreamPath}{requestModel.IIIFImageRequest.ImageRequestPath}";
        IProxyActionResult proxyActionResult = specialServer
            ? new ProxyActionResult(ProxyDestination.SpecialServer, orchestrationImage.RequiresAuth, imageServerPath)
            : new ProxyImageServerResult(orchestrationImage, orchestrationImage.RequiresAuth, imageServerPath);
        return proxyActionResult;
    }

    /// <summary>
    /// Get the ImageApi version to serve. This will return either:
    /// - The version requested in the path
    /// - Null if a specific version requested in path but it cannot be handled
    /// - Default version from appconfig
    /// </summary>
    private IIIF.ImageApi.Version? GetImageApiVersion(ImageAssetDeliveryRequest requestModel) 
        => requestModel.VersionPathValue.HasText()
            ? requestModel.VersionPathValue.ParseToIIIFImageApiVersion()
            : orchestratorSettings.Value.DefaultIIIFImageVersion;

    private async Task SetCustomHeaders(OrchestrationImage orchestrationImage,
        ProxyActionResult proxyActionResult)
    {
        var customerHeaders = (await customHeaderRepository.GetForCustomer(orchestrationImage.AssetId.Customer))
            .ToList();

        CustomHeaderProcessor.SetProxyImageHeaders(customerHeaders, orchestrationImage, proxyActionResult);

        if (orchestratorSettings.Value.Proxy.AddProxyDebugHeaders)
        {
            proxyActionResult.WithHeader("x-proxy-destination", proxyActionResult.Target.ToString());
        }
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