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
    public static ProxyImageRequest GetProxyImageRequest(this ImageRequest imageRequest, Version imageVersion,
        Size imageSize, int maxWidth, bool strictMode = true)
    {
        // Safety first - gather values + validate, only carrying on with main logic if good to proceed
        var isV2 = imageVersion == Version.V2;
        var isExplicitFull = imageRequest.IsExplicitFullSize();

        var failedValidationError = TryValidateRequest(imageRequest, isV2, isExplicitFull, strictMode);
        if (failedValidationError != null) return failedValidationError;
        
        return HandleProxyLogic(imageRequest, imageSize, maxWidth, isV2, isExplicitFull);
    }

    private static ProxyImageRequest? TryValidateRequest(ImageRequest imageRequest, bool isV2, bool isExplicitFull,
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
        int maxWidth, bool isV2, bool isExplicitFull)
    {
        try
        {
            // Get the size of the extracted region - we need this regardless of version
            var sizeParameter = imageRequest.Size; 
            var extractedRegionSize = imageRequest.Region.GetExtractedRegionSize(imageSize);
            var requestedFullRegion = imageRequest.Region.IsFullOrEquivalent(imageSize);

            // If this is not /full/ or /max/ then we won't change the size parameter, only need to check the size is valid
            if (!sizeParameter.Max)
            {
                var requestedSize = GetRequestedSize(isV2, sizeParameter, extractedRegionSize);
                if (requestedSize.MaxDimension > maxWidth)
                {
                    return ProxyImageRequest.Invalid(
                        $"Requested size '{sizeParameter}' exceeds maxWidth of {maxWidth}",
                        HttpStatusCode.Forbidden);
                }

                return ProxyImageRequest.Valid(sizeParameter, requestedSize, requestedFullRegion);
            }
            
            // If here, it's /full/ or /max/ size. In which case we will change size parameter in proxy request to be
            // the explicitly requested size (e.g. /full/ or /max/ => /1049,2033/ or /^1049,2033/)
            if (!IsUpscalingAllowed(isV2, isExplicitFull, sizeParameter))
            {
                // If no upscaling then we can just confine the size to maxWidth without attempting to grow
                return GetProxyImageRequest(Size.Confine(maxWidth, extractedRegionSize), requestedFullRegion, false);
            }

            // If upscaling is allowed, work out the final size - the largest possible size that fits withing maxWidth
            var finalSize = Size.FitWithin(Size.Square(maxWidth), extractedRegionSize);
            var upscaleProxyRequest = maxWidth > extractedRegionSize.MaxDimension && !isV2;
            return GetProxyImageRequest(finalSize, requestedFullRegion, upscaleProxyRequest);
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

    // Upscaling is allowed if v3 and '^' OR v2 and 'max' 
    private static bool IsUpscalingAllowed(bool isVersion2, bool isExplicitFullSize, SizeParameter sizeParameter) =>
        sizeParameter.Upscaled && !isVersion2 || (!isExplicitFullSize && isVersion2);

    private static Size GetRequestedSize(bool isVersion2, SizeParameter sizeParameter, Size extractedRegionSize)
    {
        // SizeParameter.Parse() works by V3 rules - so you need ^ to upscale. If we're version2, fake that briefly to
        // ease calculation. Create a new copy of SizeParameter to avoid mutating the original
        var workingSizeParam = sizeParameter;
        if (isVersion2)
        {
            workingSizeParam = SizeParameter.Parse(sizeParameter.ToString());
            workingSizeParam.Upscaled = true;
        }

        var requestedSize = workingSizeParam.Resize(extractedRegionSize);
        return requestedSize;
    }

    private static ProxyImageRequest GetProxyImageRequest(Size finalSize, bool requestedFullRegion, bool upscaled)
        => ProxyImageRequest.Valid(new SizeParameter
        {
            Width = finalSize.Width,
            Height = finalSize.Height,
            Upscaled = upscaled
        }, finalSize, requestedFullRegion);
}

/// <summary>
/// Class represents validated image proxy destination
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class ProxyImageRequest
{
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

    public static ProxyImageRequest Valid(SizeParameter sizeParameter, Size proposedSize, bool representsFullRegion) =>
        new()
        {
            RequestedSize = proposedSize,
            ProxySizeParameter = sizeParameter,
            IsValid = true,
            RepresentsFullRegion = representsFullRegion,
        };
    
    private string DebuggerDisplay => IsValid 
        ? $"Valid: {ProxySizeParameter}, {RequestedSize}"
        : $"Invalid: {ErrorStatusCode}";
}

