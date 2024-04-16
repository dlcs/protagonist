using System;
using System.Collections.Generic;
using DLCS.Core.Guard;
using DLCS.Model.Policies;
using IIIF;
using IIIF.ImageApi;

namespace DLCS.Model.Assets;

/// <summary>
/// A collection of extension methods for <see cref="Asset"/> objects.
/// </summary>
public static class AssetX
{
    /// <summary>
    /// Get a list of all available thumbnail sizes for asset, based on thumbnail policy. 
    /// </summary>
    /// <param name="asset">Asset to extract thumbnails sizes for.</param>
    /// <param name="sizeParameters">List of thumbnail policy sizes used to calculate thumb sizes.</param>
    /// <param name="maxDimensions">A tuple of maxBoundedSize, maxAvailableWidth and maxAvailableHeight.</param>
    /// <param name="includeUnavailable">Whether to include unavailable sizes or not.</param>
    /// <returns>List of available thumbnail <see cref="Size"/></returns>
    public static List<Size> GetAvailableThumbSizes(this Asset asset, List<SizeParameter> sizeParameters,
        out (int maxBoundedSize, int maxAvailableWidth, int maxAvailableHeight) maxDimensions,
        bool includeUnavailable = false)
    {
        asset.ThrowIfNull(nameof(asset));
        sizeParameters.ThrowIfNull(nameof(sizeParameters));
        
        var availableSizes = new List<Size>(sizeParameters.Count);
        var generatedMax = new List<int>(sizeParameters.Count);

        var assetSize = new Size(asset.Width.ThrowIfNull(nameof(asset.Width)),
            asset.Height.ThrowIfNull(nameof(asset.Height)));

        int maxBoundedSize = 0;
        int maxAvailableWidth = 0;
        int maxAvailableHeight = 0;

        foreach (var sizeParameter in sizeParameters)
        {
            if (!sizeParameter.Confined) continue;

            var maxConfinedDimension = Math.Max(sizeParameter.Width ?? 0, sizeParameter.Height ?? 0);
            var assetIsUnavailableForSize = AssetIsUnavailableForSize(asset, maxConfinedDimension);
            if (!includeUnavailable && assetIsUnavailableForSize) continue;
            
            Size bounded = Size.Confine(maxConfinedDimension, assetSize);
            
            var boundedMaxDimension = bounded.MaxDimension;

            // If image < thumb-size then boundedMax may already have been processed (it'll be the same as imageMax)
            if (generatedMax.Contains(boundedMaxDimension)) continue;
            
            generatedMax.Add(boundedMaxDimension);
            availableSizes.Add(bounded);
            if (maxConfinedDimension > maxBoundedSize && !assetIsUnavailableForSize)
            {
                maxBoundedSize = Math.Min(maxConfinedDimension, boundedMaxDimension); // handles image being smaller than thumb
                maxAvailableWidth = bounded.Width;
                maxAvailableHeight = bounded.Height;
            }
        }

        maxDimensions = (maxBoundedSize, maxAvailableWidth, maxAvailableHeight);
        return availableSizes;
    }

    /// <summary>
    /// Reset fields for ingestion, marking as "Ingesting" and clearing errors
    /// </summary>
    public static void SetFieldsForIngestion(this Asset asset)
    {
        asset.Error = string.Empty;
        asset.Ingesting = true;
    }

    /// <summary>
    /// Mark asset as finished, setting "Finished" and "Ingesting" = false 
    /// </summary>
    public static void MarkAsFinished(this Asset asset)
    {
        asset.Ingesting = false;
        asset.Finished = DateTime.UtcNow;
    }

    private static bool AssetIsUnavailableForSize(Asset asset, int boundingSize)
        => asset.RequiresAuth && boundingSize > asset.MaxUnauthorised;
}