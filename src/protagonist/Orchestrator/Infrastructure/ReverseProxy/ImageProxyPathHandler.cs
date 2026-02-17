using System;
using System.Diagnostics.CodeAnalysis;
using DLCS.Model.IIIF;
using IIIF;
using IIIF.Exceptions;
using IIIF.ImageApi;

namespace Orchestrator.Infrastructure.ReverseProxy;

/// <summary>
/// Contains decision making logic for proxying image requests.
/// </summary>
public static class ImageProxyPathHandler
{
    /// <summary>
    /// Get the proposed final size and <see cref="SizeParameter"/> image request based on incoming imageRequest and maxWidth.
    /// </summary>
    /// <param name="imageRequest">Incoming <see cref="ImageRequest"/></param>
    /// <param name="imageSize">The <see cref="Size"/> of source image</param>
    /// <param name="maxWidth">The effective maxWidth value for image</param>
    /// <returns>
    /// <see cref="ProxySizeResult"/> containing effective size, validity and proxy <see cref="SizeParameter"/>
    /// </returns>
    public static ProxySizeResult GetProposedFinalSize(this ImageRequest imageRequest, Size imageSize, int maxWidth)
    {
        try
        {
            // Get the size of the extracted region
            var extractedRegionSize = imageRequest.Region.GetExtractedRegionSize(imageSize);
            var sizeParameter = imageRequest.Size;
            
            // If this is not /full/ or /max/ then we won't change the size parameter, only need to check the size 
            if (!sizeParameter.Max)
            {
                var requestedSize = sizeParameter.Resize(extractedRegionSize, InvalidUpscaleBehaviour.Throw);
                return requestedSize.MaxDimension > maxWidth
                    ? ProxySizeResult.Invalid($"Requested size '{sizeParameter}' exceeds maxWidth of {maxWidth}")
                    : ProxySizeResult.Valid(sizeParameter, requestedSize);
            }

            // If /full/ or /max/ then we will change size-parameter to be explicit requested size
            if (sizeParameter.Upscaled)
            {
                // ^full isn't valid
                if (imageRequest.IsExplicitFullSize())
                {
                    return ProxySizeResult.Invalid("'^full' size is invalid. Use 'full' or '^max' instead.");
                }
                    
                // Work out the final size - this will be the largest that fits withing maxWidth confinement, possibly
                // growing
                var finalSize = Size.FitWithin(Size.Square(maxWidth), extractedRegionSize);
                return GetProxySizeResult(finalSize, maxWidth > extractedRegionSize.MaxDimension);
            }
            else
            {
                // Work out the final size - this will be the extracted region size or that size confined to maxWidth 
                var finalSize = Size.Confine(maxWidth, extractedRegionSize);
                return GetProxySizeResult(finalSize);
            }
        }
        catch (RegionException ex)
        {
            return ProxySizeResult.Invalid(ex.Message); // TODO - differentiate between 400/403/401?
        }
        catch (InvalidOperationException ex)
        {
            return ProxySizeResult.Invalid(ex.Message);
        }
    }

    private static ProxySizeResult GetProxySizeResult(Size finalSize, bool upscaled = false)
        => ProxySizeResult.Valid(new SizeParameter
        {
            Width = finalSize.Width,
            Height = finalSize.Height,
            Upscaled = upscaled
        }, finalSize);
}

/// <summary>
/// Class represents 
/// </summary>
public class ProxySizeResult
{
    /// <summary>
    /// <see cref="SizeParameter"/> that should be used to proxy image request.
    /// </summary>
    [MemberNotNullWhen(true, nameof(IsValid))]
    public SizeParameter? ProxySizeParameter { get; private init; }
        
    /// <summary>
    /// <see cref="Size"/> that represents the final size.
    /// </summary>
    [MemberNotNullWhen(true, nameof(IsValid))]
    public Size? RequestedSize { get; private init; }
    
    /// <summary>
    /// Whether the proxy request is valid.
    /// </summary>
    public bool IsValid { get; private init; }
    
    /// <summary>
    /// Error message representing why request is invalid.
    /// </summary>
    [MemberNotNullWhen(false, nameof(IsValid))]
    public string? ErrorMessage { get; private init; }

    public static ProxySizeResult Invalid(string message) => new() { IsValid = false, ErrorMessage = message };

    public static ProxySizeResult Valid(SizeParameter sizeParameter, Size proposedSize) => new()
    {
        RequestedSize = proposedSize,
        ProxySizeParameter = sizeParameter,
        IsValid = true
    };
}

