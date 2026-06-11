using System;
using System.Collections.Generic;
using DLCS.Core.Guard;
using DLCS.Model.IIIF;
using IIIF;
using IIIF.ImageApi;

namespace DLCS.Model.Assets;

/// <summary>
/// A collection of extension methods for <see cref="Asset"/> objects.
/// </summary>
public static class AssetX
{
    /// <summary>
    /// Get a list of all thumbnail sizes for asset, based on IIIF SizeParameter 
    /// </summary>
    /// <param name="asset">Asset to extract thumbnails sizes for.</param>
    /// <param name="sizeParameters">List of thumbnail policy sizes used to calculate thumb sizes.</param>
    /// <param name="systemMaxWidth">The system default maxWidth.</param>
    /// <returns>List of available thumbnail <see cref="Size"/></returns>
    public static ThumbnailSizes GetAvailableThumbSizes(this Asset asset, List<SizeParameter> sizeParameters, 
        int systemMaxWidth)
    {
        asset.ThrowIfNull(nameof(asset));
        sizeParameters.ThrowIfNull(nameof(sizeParameters));
        
        var generatedMax = new List<int>(sizeParameters.Count);

        var assetSize = new Size(asset.Width.ThrowIfNull(nameof(asset.Width)),
            asset.Height.ThrowIfNull(nameof(asset.Height)));

        var thumbnailSizes = new ThumbnailSizes(sizeParameters.Count);
        
        // Get the largest possible open size, this is the maximum thumbnail size
        var largestThumbnailSize = asset.GetLargestOpenFullSize(systemMaxWidth);

        foreach (var sizeParameter in sizeParameters)
        {
            if (!IsValidForCalculation(assetSize, sizeParameter)) continue;
            
            var resized = sizeParameter.ResizeIfSupported(assetSize);
            var maxDimension = resized.MaxDimension;
            
            // If image < thumb-size then boundedMax may already have been processed, it'll be the same as imageMax as 
            // we don't support sizes that alter aspect ratio
            if (generatedMax.Contains(maxDimension)) continue;
            generatedMax.Add(maxDimension);
            
            if (maxDimension > largestThumbnailSize)
            {
                thumbnailSizes.AddAuth(resized);
            }
            else
            {
                thumbnailSizes.AddOpen(resized);
            }
        }

        return thumbnailSizes;
    }

    private static bool IsValidForCalculation(Size imageSize, SizeParameter sizeParameter)
    {
        // /!w,h/ is applicable for calculating as we will use the max, which will be the confined value
        if (sizeParameter.Confined) return true;

        var imageShape = imageSize.GetShape();
        
        // /w,/ is applicable for Landscape or Square images as width will be fixed as it's longest edge
        if (sizeParameter.Width.HasValue && imageShape != ImageShape.Portrait) return true;
        
        // /,h/ is applicable for Portait or Square images as width will be fixed as it's longest edge
        if (sizeParameter.Height.HasValue && imageShape != ImageShape.Landscape) return true;

        // any other combination is invalid as we store thumbs by longest so rounding error could affect finding image 
        return false;
    }

    /// <summary>
    /// Mark asset as finished, setting "Finished" and "Ingesting" = false 
    /// </summary>
    public static void MarkAsFinished(this Asset asset)
    {
        asset.Ingesting = false;
        asset.Finished = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates the longest edge value for an open /full/ region request, based on maxWidth, openFullMax and Roles.
    /// </summary>
    /// <param name="asset">The asset for which to determine the longest edge of the open full region.</param>
    /// <param name="systemMaxWidth">The system default maxWidth.</param>
    /// <returns>The longest edge value for the open full region, or 0 if no open thumbnails are available.</returns>
    /// <remarks>
    /// This is used to determine the largest size available for Orchestrator requests and thumb generation
    /// </remarks>
    public static int GetLargestOpenFullSize(this Asset asset, int systemMaxWidth)
    {
        // The effective MaxWidth value can be from the Asset or the system-wide default
        var effectiveMaxWidth = asset.GetEffectiveMaxWidth(systemMaxWidth);
        
        // If no role, the only restriction is maxWidth (openFullMax is ignored)
        if (!asset.HasRoles) return effectiveMaxWidth;

        // If OpenFullMax == 0 then there are no "open" full sizes, because we have role(s)
        if ((asset.OpenFullMax ?? 0) == 0) return 0;

        // We have an OpenFullMax value, if we also have MaxWidth return the smallest of that an OpenFullMax
        return Math.Min(effectiveMaxWidth, asset.OpenFullMax!.Value);
    }

    /// <summary>
    /// Get the effective maxWidth value for asset, taking into account the system default maxWidth
    /// </summary>
    public static int GetEffectiveMaxWidth(this Asset asset, int systemMaxWidth)
        => (asset.MaxWidth ?? 0) == 0
            ? systemMaxWidth
            : Math.Min(asset.MaxWidth!.Value, systemMaxWidth);
}
