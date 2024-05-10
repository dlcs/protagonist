using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Model.Policies;

namespace DLCS.Model.Assets;

public static class ImageDeliveryChannelX
{
    /// <summary>
    /// Get ImageDeliveryChannel record for Thumbs channel, optionally throwing if not found.
    /// </summary>
    /// <param name="imageDeliveryChannels">Collection of <see cref="DeliveryChannelPolicy"/></param>
    /// <param name="throwIfNotFound">If true, and thumbs not found, then exception will the thrown</param>
    /// <returns><see cref="DeliveryChannelPolicy"/>, if found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if thumbs policy not found and throwIfNotFound = true</exception>
    public static ImageDeliveryChannel? GetThumbsChannel(
        this ICollection<ImageDeliveryChannel> imageDeliveryChannels,
        bool throwIfNotFound = false)
    {
        if (imageDeliveryChannels.IsNullOrEmpty())
        {
            return throwIfNotFound
                ? throw new InvalidOperationException("Thumbs policy not found")
                : null;
        }

        return throwIfNotFound
            ? imageDeliveryChannels.Single(dcp => dcp.Channel == AssetDeliveryChannels.Thumbnails)
            : imageDeliveryChannels.SingleOrDefault(dcp => dcp.Channel == AssetDeliveryChannels.Thumbnails);
    }
}