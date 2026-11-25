using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using DLCS.Model.IIIF;
using IIIF;
using IIIF.Exceptions;
using IIIF.ImageApi;
using Version = IIIF.ImageApi.Version;

namespace Orchestrator.Infrastructure.ReverseProxy;

/// <summary>
/// Contains decision making logic for proxying image requests.
/// </summary>
public static class ImageProxyPathHandler
{
    /// <summary>
    /// Get the proposed final size and <see cref="SizeParameter"/> for image requests based on incoming imageRequest
    /// and effective maxWidth. The returned result may be invalid if request cannot be fulfilled.
    /// </summary>
    /// <param name="imageRequest">Incoming <see cref="ImageRequest"/></param>
    /// <param name="imageVersion">IIIF ImageApi version this request is for</param>
    /// <param name="imageSize">The <see cref="Size"/> of source image</param>
    /// <param name="maxWidth">The effective maxWidth value for image</param>
    /// <param name="strictMode">
    /// If true, strictly process in accordance with appropriate IIIF ImageApi version. If false, use 'lax' processing
    /// which supports 'full' request for V3 requests. See ADR 0011.
    /// </param>
    /// <returns>
    /// <see cref="ProxyImageRequest"/> containing effective size, validity and proxy <see cref="SizeParameter"/>
    /// </returns>
    /// <param name="allowUpscaling">
    /// If false, no request is forwarded larger than its extracted region: a v2 size larger than the region is forwarded
    /// at the region's own size (keeping the requested aspect ratio); a v3 '^' size larger than the region is a 400 (the
    /// info.json does not list 'sizeUpscaling'); v3 '^max' is a 400 if maxWidth permits more than the region (the
    /// spec defines '^max' as that upscale), else the region confined to maxWidth. See ADR 0011.
    /// </param>
    public static ProxyImageRequest GetProxyImageRequest(this ImageRequest imageRequest, Version imageVersion,
        Size imageSize, int maxWidth, bool strictMode = true, bool allowUpscaling = true)
    {
        // Safety first - gather values + validate, only carrying on with main logic if good to proceed
        var isV2 = imageVersion == Version.V2;
        var isExplicitFull = imageRequest.IsExplicitFullSize();

        var failedValidationError = GetValidationFailure(imageRequest, isV2, isExplicitFull, strictMode);
        if (failedValidationError != null) return failedValidationError;
        
        return HandleProxyLogic(imageRequest, imageSize, maxWidth, isV2, allowUpscaling);
    }

    private static ProxyImageRequest? GetValidationFailure(ImageRequest imageRequest, bool isV2, bool isExplicitFull,
        bool strictMode)
    {
        // V2 doesn't support ^ character, ever
        if (isV2)
        {
            if (imageRequest.Size.Upscaled)
            {
                return ProxyImageRequest.Invalid("Invalid size. '^' invalid for IIIF ImageApi 2.1",
                    HttpStatusCode.BadRequest);
            }
        }
        else
        {
            // V3 doesn't support /full/ size unless we're in 'lax' mode
            if (isExplicitFull && (strictMode || imageRequest.Size.Upscaled))
            {
                return ProxyImageRequest.Invalid("Invalid size. 'full' invalid for IIIF ImageApi 3.0",
                    HttpStatusCode.BadRequest);
            }
        }

        return null;
    }

    private static ProxyImageRequest HandleProxyLogic(ImageRequest imageRequest, Size imageSize,
        int maxWidth, bool isV2, bool allowUpscaling)
    {
        try
        {
            // Get the size of the extracted region - we need this regardless of version
            var sizeParameter = imageRequest.Size; 
            var extractedRegionSize = imageRequest.Region.GetExtractedRegionSize(imageSize);
            
            // Region may extend beyond image bounds, crop to image edge to avoid passing invalid region downstream
            var proxyRegionParameter = imageRequest.Region.ConfineToImageBounds(imageSize);
            var requestedFullRegion = proxyRegionParameter.IsFullOrEquivalent(imageSize);

            // If this is not /full/ or /max/ then need to check the size is valid. May rewrite confined size requests
            if (!sizeParameter.Max)
            {
                var (requestedSize, proxySizeParameter) = GetRequestedSize(isV2, sizeParameter, extractedRegionSize, maxWidth);

                // Without upscaling no size larger than the region is forwarded, so the image server never renders an
                // enlargement. v2 (where upscaling is ill-defined): clamped to the region's own size, keeping the
                // requested aspect ratio - e.g. /full/,10000/ of a 1425x2722 image is forwarded as /full/1425,2722/.
                // v3: '^' is explicit, and info.json does not list the 'sizeUpscaling' feature, so it is a 400.
                if (!allowUpscaling && (requestedSize.Width > extractedRegionSize.Width ||
                                        requestedSize.Height > extractedRegionSize.Height))
                {
                    if (!isV2)
                    {
                        return ProxyImageRequest.Invalid(
                            $"Invalid size '{sizeParameter}': upscaling is not supported ('sizeUpscaling' is not listed)",
                            HttpStatusCode.BadRequest);
                    }

                    // w, ,h pct: and !w,h keep the region's aspect ratio, so the answer is the region itself (Confine
                    // would round from an already-truncated size and can come out a pixel short); an explicit w,h may be
                    // distorted, so it keeps its own aspect ratio, shrunk to fit the region
                    var explicitWidthAndHeight = sizeParameter is { Width: not null, Height: not null, Confined: false };
                    requestedSize = explicitWidthAndHeight
                        ? Size.Confine(extractedRegionSize, requestedSize)
                        : extractedRegionSize;
                    proxySizeParameter = CreateExactSizeParameter(requestedSize, false);
                }

                if (requestedSize.MaxDimension > maxWidth)
                {
                    return ProxyImageRequest.Invalid(
                        $"Requested size '{sizeParameter}' exceeds maxWidth of {maxWidth}",
                        HttpStatusCode.Forbidden);
                }

                return ProxyImageRequest.Valid(proxyRegionParameter, proxySizeParameter, requestedSize,
                    requestedFullRegion);
            }
            
            // If here, it's /full/ or /max/ size. In which case we will change size parameter in proxy request to be
            // the explicitly requested size (e.g. /full/ or /max/ => /1049,2033/ or /^1049,2033/)

            // v3 '^max' is "scaled to the maximum size permitted by maxWidth ... If the resulting dimensions are greater
            // than the extracted region, the extracted region is upscaled". Without upscaling that is a 400 - unless
            // maxWidth permits no more than the region, when '^max' is the region confined to maxWidth
            if (!isV2 && sizeParameter.Upscaled && !allowUpscaling && maxWidth > extractedRegionSize.MaxDimension)
            {
                return ProxyImageRequest.Invalid(
                    $"Invalid size '{sizeParameter}': upscaling is not supported ('sizeUpscaling' is not listed)",
                    HttpStatusCode.BadRequest);
            }

            if (!IsUpscalingAllowed(isV2, sizeParameter, allowUpscaling))
            {
                // If no upscaling then we can just confine the size to maxWidth without attempting to grow
                return GetProxyImageRequest(proxyRegionParameter, Size.Confine(maxWidth, extractedRegionSize),
                    requestedFullRegion, false);
            }

            // If upscaling is allowed (v3 '^max' only), work out the final size - the largest possible size that fits
            // within maxWidth
            var finalSize = Size.FitWithin(Size.Square(maxWidth), extractedRegionSize);
            var upscaleProxyRequest = maxWidth > extractedRegionSize.MaxDimension;
            return GetProxyImageRequest(proxyRegionParameter, finalSize, requestedFullRegion, upscaleProxyRequest);
        }
        catch (RegionException ex)
        {
            return ProxyImageRequest.Invalid(ex.Message, HttpStatusCode.BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return ProxyImageRequest.Invalid(ex.Message, HttpStatusCode.BadRequest);
        }
    }

    // Upscaling is only allowed for v3 '^max', and only if the deployment allows upscaling at all. v2 'max' behaves
    // like v3 'max': the extracted region, confined to maxWidth but never grown (see ADR 0011). ImageApi 2.1 defines
    // 'max' as the "maximum size available, as indicated by maxWidth..." - read literally, that grew every v2 '/max/'
    // up to maxWidth.
    private static bool IsUpscalingAllowed(bool isVersion2, SizeParameter sizeParameter, bool allowUpscaling) =>
        allowUpscaling && sizeParameter.Upscaled && !isVersion2;

    private static (Size Size, SizeParameter SizeParameter) GetRequestedSize(bool isVersion2,
        SizeParameter sizeParameter, Size extractedRegionSize, int maxWidth)
    {
        var workingSizeParam = GetWorkingSizeParam(isVersion2, sizeParameter);

        // If it's not confined, return calculate final size and return
        if (!sizeParameter.Confined)
        {
            return (workingSizeParam.Resize(extractedRegionSize), sizeParameter);
        }

        // If it's confined + would exceed the maxWidth then resize down to "as large as possible but not larger than
        // server-imposed limits"
        if (Math.Max(sizeParameter.Width ?? 0, sizeParameter.Height ?? 0) > maxWidth)
        {
            workingSizeParam.Width = maxWidth;
            workingSizeParam.Height = maxWidth;
        }

        var requestedSize = workingSizeParam.Resize(extractedRegionSize, InvalidUpscaleBehaviour.ReturnOriginal);
        return (requestedSize, CreateExactSizeParameter(requestedSize, sizeParameter.Upscaled));
    }

    private static SizeParameter GetWorkingSizeParam(bool isVersion2, SizeParameter sizeParameter)
    {
        // SizeParameter.Parse() works by V3 rules - so you need ^ to upscale. If we're version2, fake that briefly to
        // ease calculation. Create a new copy of SizeParameter to avoid mutating the original
        if (!isVersion2) return sizeParameter;

        var workingSizeParam = SizeParameter.Parse(sizeParameter.ToString());
        workingSizeParam.Upscaled = true;
        return workingSizeParam;
    }

    private static ProxyImageRequest GetProxyImageRequest(RegionParameter regionParameter, Size finalSize,
        bool requestedFullRegion, bool upscaled)
        => ProxyImageRequest.Valid(regionParameter, CreateExactSizeParameter(finalSize, upscaled), finalSize,
            requestedFullRegion);

    private static SizeParameter CreateExactSizeParameter(Size finalSize, bool upscaled) =>
        new()
        {
            Width = finalSize.Width,
            Height = finalSize.Height,
            Upscaled = upscaled
        };
}

/// <summary>
/// Class represents validated image proxy destination
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class ProxyImageRequest
{
    /// <summary>
    /// <see cref="RegionParameter"/> that should be used to proxy image request.
    /// </summary>
    public RegionParameter? ProxyRegionParameter { get; private init; }
    
    /// <summary>
    /// <see cref="SizeParameter"/> that should be used to proxy image request.
    /// </summary>
    public SizeParameter? ProxySizeParameter { get; private init; }
        
    /// <summary>
    /// <see cref="Size"/> that represents the final size.
    /// </summary>
    public Size? RequestedSize { get; private init; }
    
    /// <summary>
    /// Whether the proxy request is valid.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ProxyRegionParameter))]
    [MemberNotNullWhen(true, nameof(ProxySizeParameter))]
    [MemberNotNullWhen(true, nameof(RequestedSize))]
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    [MemberNotNullWhen(false, nameof(ErrorStatusCode))]
    public bool IsValid { get; private init; }
    
    /// <summary>
    /// HTTP status code representing why request is invalid.
    /// </summary>
    public HttpStatusCode? ErrorStatusCode { get; private init; }
    
    /// <summary>
    /// Error message representing why request is invalid.
    /// </summary>
    public string? ErrorMessage { get; private init; }
    
    /// <summary>
    /// True if the request region represents the full image size.
    /// </summary>
    public bool RepresentsFullRegion { get; private init; }
    
    public static ProxyImageRequest Invalid(string message, HttpStatusCode statusCode)
        => new() { IsValid = false, ErrorMessage = message, ErrorStatusCode = statusCode };

    public static ProxyImageRequest Valid(RegionParameter regionParameter, SizeParameter sizeParameter,
        Size proposedSize, bool representsFullRegion) =>
        new()
        {
            ProxyRegionParameter = regionParameter,
            RequestedSize = proposedSize,
            ProxySizeParameter = sizeParameter,
            IsValid = true,
            RepresentsFullRegion = representsFullRegion,
        };
    
    private string DebuggerDisplay => IsValid 
        ? $"Valid: {ProxyRegionParameter}, {ProxySizeParameter}, {RequestedSize}"
        : $"Invalid: {ErrorStatusCode}";
}

