﻿using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Policies;
using IIIF;

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
    /// <param name="thumbnailPolicy">The thumbnail policy to use to calculate thumb sizes.</param>
    /// <param name="maxDimensions">A tuple of maxBoundedSize, maxAvailableWidth and maxAvailableHeight.</param>
    /// <param name="includeUnavailable">Whether to include unavailable sizes or not.</param>
    /// <returns>List of available thumbnail <see cref="Size"/></returns>
    public static List<Size> GetAvailableThumbSizes(this Asset asset, ThumbnailPolicy thumbnailPolicy,
        out (int maxBoundedSize, int maxAvailableWidth, int maxAvailableHeight) maxDimensions,
        bool includeUnavailable = false)
    {
        asset.ThrowIfNull(nameof(asset));
        thumbnailPolicy.ThrowIfNull(nameof(thumbnailPolicy));

        var availableSizes = new List<Size>(thumbnailPolicy.SizeList.Count);
        var generatedMax = new List<int>(thumbnailPolicy.SizeList.Count);

        var size = new Size(asset.Width.ThrowIfNull(nameof(asset.Width)),
            asset.Height.ThrowIfNull(nameof(asset.Height)));

        int maxBoundedSize = 0;
        int maxAvailableWidth = 0;
        int maxAvailableHeight = 0;

        foreach (int boundingSize in thumbnailPolicy.SizeList)
        {
            var assetIsUnavailableForSize = AssetIsUnavailableForSize(asset, boundingSize);
            if (!includeUnavailable && assetIsUnavailableForSize) continue;

            Size bounded = Size.Confine(boundingSize, size);

            var boundedMaxDimension = bounded.MaxDimension;
            
            // If image < thumb-size then boundedMax may already have been processed (it'll be the same as imageMax)
            if (generatedMax.Contains(boundedMaxDimension)) continue;
            
            generatedMax.Add(boundedMaxDimension);
            availableSizes.Add(bounded);
            if (boundingSize > maxBoundedSize && !assetIsUnavailableForSize)
            {
                maxBoundedSize = Math.Min(boundingSize, boundedMaxDimension); // handles image being smaller than thumb
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