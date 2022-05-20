﻿using System.Collections.Generic;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using IIIF;

namespace DLCS.Model.Assets
{
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

            var availableSizes = new List<Size>();

            var size = new Size(asset.Width, asset.Height);

            int maxBoundedSize = 0;
            int maxAvailableWidth = 0;
            int maxAvailableHeight = 0;

            foreach (int boundingSize in thumbnailPolicy.SizeList)
            {
                var assetIsUnavailableForSize = AssetIsUnavailableForSize(asset, boundingSize);
                if (!includeUnavailable && assetIsUnavailableForSize)
                {
                    continue;
                }

                Size bounded = Size.Confine(boundingSize, size);
                availableSizes.Add(bounded);
                if (boundingSize > maxBoundedSize && !assetIsUnavailableForSize)
                {
                    maxBoundedSize = boundingSize;
                    maxAvailableWidth = bounded.Width;
                    maxAvailableHeight = bounded.Height;
                }
            }

            maxDimensions = (maxBoundedSize, maxAvailableWidth, maxAvailableHeight);
            return availableSizes;
        }

        /// <summary>
        /// Get <see cref="AssetId"/> for asset
        /// </summary>
        /// <param name="asset">Current <see cref="Asset"/></param>
        /// <returns><see cref="AssetId"/> containing current customer, space, asset id</returns>
        public static AssetId GetAssetId(this Asset asset)
            => AssetId.FromString(asset.Id);

        private static bool AssetIsUnavailableForSize(Asset asset, int boundingSize)
            => asset.RequiresAuth && boundingSize > asset.MaxUnauthorised;
    }
}