using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Model.IIIF;
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
using Version = IIIF.ImageApi.Version;

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

        if (!IsSizeValid(assetRequest.IIIFImageRequest.Size))
        {
            logger.LogDebug("Request for {Path}: invalid size", httpContext.Request.Path);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
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

    private static bool IsSizeValid(SizeParameter size) => (size.Width ?? 1) > 0 && (size.Height ?? 1) > 0;

    private async Task<IProxyActionResult> HandleRequestInternal(HttpContext httpContext,
        OrchestrationImage orchestrationImage, ImageAssetDeliveryRequest assetRequest)
    {
        // Get the requested version
        var imageApiVersion = GetImageApiVersion(assetRequest);
        if (imageApiVersion == null)
        {
            logger.LogDebug("Unable to fulfil image request: {Path}. Could not parse ImageVersion",
                assetRequest.NormalisedFullPath);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }
        
        var imageSize = new Size(orchestrationImage.Width, orchestrationImage.Height);
        
        // Get the proposed image size - this is required to determine if we will exceed this
        var incomingImageRequest = assetRequest.IIIFImageRequest;
        var proxyRequest =
            incomingImageRequest.GetProxyImageRequest(imageApiVersion.Value, imageSize, orchestrationImage.MaxWidth);
        if (!proxyRequest.IsValid)
        {
            return new StatusCodeResult(proxyRequest.ErrorStatusCode.Value);
        }
        
        var requestedFullRegion =
            incomingImageRequest.Region.IsFullOrEquivalent(orchestrationImage.Width,
                orchestrationImage.Height);
        
        // If there are roles, we may have restricted access..
        if (orchestrationImage.RequiresAuth)
        {
            if (await IsRequestUnauthorised(assetRequest, orchestrationImage, requestedFullRegion, proxyRequest))
            {
                return new StatusCodeResult(HttpStatusCode.Unauthorized);
            }
        }
        
        if (requestedFullRegion)
        {
            // /full/ or equiv region but not /max/ size - can it be handled by thumbnail service?
            if (!incomingImageRequest.Size.Max)
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
            if (orchestrationImage.S3Location.IsNullOrEmpty())
            {
                // Rare occurence - fall through to image server which will handle reingest request
                logger.LogInformation("{AssetId} candidate for SpecialServer handling but s3Location empty",
                    orchestrationImage.AssetId);
            }
            else
            {
                return GetImageServerProxyResult(true);
            }
        }
        
        // Fallback to image-server, with orchestration if required
        return GetImageServerProxyResult(false);

        IProxyActionResult GetImageServerProxyResult(bool specialServer) =>
            GenerateImageServerProxyResult(orchestrationImage, assetRequest, proxyRequest, imageApiVersion.Value,
                specialServer);
    }

    private async Task<bool> IsRequestUnauthorised(ImageAssetDeliveryRequest assetRequest,
        OrchestrationImage orchestrationImage, bool requestedFullRegion, ProxyImageRequest proxyRequest)
    {
        // If the image has an openFullMax, and the region is /full/ then user may be able to see requested
        // size without doing auth check
        if (requestedFullRegion && orchestrationImage.OpenFullMax > 0)
        {
            // If requested maxDimension < openFullMax then anyone can view as this is a /full/ region
            if (proxyRequest.RequestedSize!.MaxDimension <= orchestrationImage.OpenFullMax)
            {
                logger.LogDebug(
                    "Request for {ImageRequest} requires auth but viewable due to openFullMax size of {OpenFullMax}",
                    assetRequest.IIIFImageRequest.OriginalPath, orchestrationImage.OpenFullMax);
                return false;
            }
        }
        
        // IAssetAccessValidator is in container with ServiceLifetime.Scoped
        using var scope = scopeFactory.CreateScope();
        var assetAccessValidator = scope.ServiceProvider.GetRequiredService<IAssetAccessValidator>();
        var authResult = await assetAccessValidator.TryValidate(assetRequest.GetAssetId(), orchestrationImage.Roles,
            AuthMechanism.Cookie);

        return authResult == AssetAccessResult.Unauthorized;
    }

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
        ImageAssetDeliveryRequest requestModel, ProxyImageRequest proxyRequest, Version imageApiVersion, 
        bool specialServer)
    {
        // get the redirect path - S3:// path for special-server or /path/on/disk for image-server
        var settings = orchestratorSettings.Value;
        var downstreamPath = specialServer
            ? settings.GetSpecialServerPath(orchestrationImage.S3Location ?? string.Empty, imageApiVersion)
            : settings.GetImageServerPath(orchestrationImage.AssetId, imageApiVersion);

        if (string.IsNullOrEmpty(downstreamPath))
        {
            logger.LogDebug("Unable to fulfil image request: {Path}. Could not generate ImageServer path",
                requestModel.NormalisedFullPath);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }

        // Update the SizeParameter as it may have altered during parsing
        requestModel.IIIFImageRequest.Size = proxyRequest.ProxySizeParameter!;
        var imageServerPath = downstreamPath.ToConcatenated('/', requestModel.IIIFImageRequest.GetImageRequestOnly());
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
    private Version? GetImageApiVersion(ImageAssetDeliveryRequest requestModel) 
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
