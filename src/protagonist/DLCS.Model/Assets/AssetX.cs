using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DLCS.Core.Guard;
using IIIF;
using IIIF.ImageApi;

namespace DLCS.Model.Assets;

/// <summary>
/// A collection of extension methods for <see cref="Asset"/> objects.
/// </summary>
public static class AssetX
{
    public static List<Size> GetAllThumbSizes(this Asset asset)
    {
        var thumbnailSizes = new List<Size>();

        var sizeParameters = ConvertThumbnailPolicy(asset);

        thumbnailSizes = sizeParameters.Select(s => new Size(s.Width.Value, s.Height.Value)).ToList();

        return thumbnailSizes;
    }

    /// <summary>
    /// Get a list of all available thumbnail sizes for asset, based on thumbnail policy. 
    /// </summary>
    /// <param name="asset">Asset to extract thumbnails sizes for.</param>
    /// <param name="maxDimensions">A tuple of maxBoundedSize, maxAvailableWidth and maxAvailableHeight.</param>
    /// <param name="includeUnavailable">Whether to include unavailable sizes or not.</param>
    /// <returns>List of available thumbnail <see cref="Size"/></returns>
    public static List<Size> GetAvailableThumbSizes(this Asset asset,
        out (int maxBoundedSize, int maxAvailableWidth, int maxAvailableHeight) maxDimensions,
        bool includeUnavailable = false)
    {
        var thumbnailPolicy = ConvertThumbnailPolicy(asset);

        asset.ThrowIfNull(nameof(asset));
        thumbnailPolicy.ThrowIfNull(nameof(thumbnailPolicy));

        var availableSizes = new List<Size>(thumbnailPolicy.Count);
        var generatedMax = new List<int>(thumbnailPolicy.Count);

        var size = new Size(asset.Width.ThrowIfNull(nameof(asset.Width)),
            asset.Height.ThrowIfNull(nameof(asset.Height)));

        int maxBoundedSize = 0;
        int maxAvailableWidth = 0;
        int maxAvailableHeight = 0;

        foreach (var boundingSize in thumbnailPolicy)
        {
            int maxDimension = boundingSize.Width > boundingSize.Height ? 
                boundingSize.Width.Value : boundingSize.Height.Value;
            
            var assetIsUnavailableForSize = AssetIsUnavailableForSize(asset, maxDimension);
            if (!includeUnavailable && assetIsUnavailableForSize) continue;
            Size bounded;

            if (asset.HasDeliveryChannel(AssetDeliveryChannels.Image) && size.MaxDimension == 0)
            { 
                bounded = Size.Confine(maxDimension, new Size(boundingSize.Width!.Value, boundingSize.Height.Value));
            }
            else
            {
                bounded = Size.Confine(maxDimension, size);
            }
            
            var boundedMaxDimension = bounded.MaxDimension;

            // If image < thumb-size then boundedMax may already have been processed (it'll be the same as imageMax)
            if (generatedMax.Contains(boundedMaxDimension)) continue;
            
            generatedMax.Add(boundedMaxDimension);
            availableSizes.Add(bounded);
            if (maxDimension > maxBoundedSize && !assetIsUnavailableForSize)
            {
                maxBoundedSize = Math.Min(maxDimension, boundedMaxDimension); // handles image being smaller than thumb
                maxAvailableWidth = bounded.Width;
                maxAvailableHeight = bounded.Height;
            }
        }

        maxDimensions = (maxBoundedSize, maxAvailableWidth, maxAvailableHeight);
        return availableSizes;
    }

    private static List<SizeParameter> ConvertThumbnailPolicy(Asset asset)
    {
        var sizeParameters = new List<SizeParameter>();

        if (asset.HasDeliveryChannel(AssetDeliveryChannels.Thumbnails))
        {
            var initialPolicyTransformation = JsonSerializer.Deserialize<List<string>>(asset.ImageDeliveryChannels
                .Single(
                    x => x.Channel == AssetDeliveryChannels.Thumbnails)
                .DeliveryChannelPolicy.PolicyData);

            foreach (var sizeValue in initialPolicyTransformation!)
            {
                sizeParameters.Add(SizeParameter.Parse(sizeValue));
            }
        }

        return sizeParameters;
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