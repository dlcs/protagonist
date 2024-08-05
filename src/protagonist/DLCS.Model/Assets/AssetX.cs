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
    /// <returns>List of available thumbnail <see cref="Size"/></returns>
    public static ThumbnailSizes GetAvailableThumbSizes(this Asset asset, List<SizeParameter> sizeParameters)
    {
        asset.ThrowIfNull(nameof(asset));
        sizeParameters.ThrowIfNull(nameof(sizeParameters));
        
        var generatedMax = new List<int>(sizeParameters.Count);

        var assetSize = new Size(asset.Width.ThrowIfNull(nameof(asset.Width)),
            asset.Height.ThrowIfNull(nameof(asset.Height)));

        var thumbnailSizes = new ThumbnailSizes(sizeParameters.Count);

        foreach (var sizeParameter in sizeParameters)
        {
            var resized = sizeParameter.ResizeIfSupported(assetSize);
            var maxDimension = resized.MaxDimension;
            
            // If image < thumb-size then boundedMax may already have been processed, it'll be the same as imageMax as 
            // we don't support sizes that alter aspect ratio
            if (generatedMax.Contains(maxDimension)) continue;
            generatedMax.Add(maxDimension);
            
            var assetIsUnavailableForSize = AssetIsUnavailableForSize(asset, maxDimension);
            if (assetIsUnavailableForSize)
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