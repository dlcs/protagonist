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
        => GetChannel(imageDeliveryChannels, AssetDeliveryChannels.Thumbnails, "Thumbs policy not found",
            throwIfNotFound);

    /// <summary>
    /// Get ImageDeliveryChannel record for Timebased channel, optionally throwing if not found.
    /// </summary>
    /// <param name="imageDeliveryChannels">Collection of <see cref="DeliveryChannelPolicy"/></param>
    /// <param name="throwIfNotFound">If true, and timebased not found, then exception will the thrown</param>
    /// <returns><see cref="DeliveryChannelPolicy"/>, if found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if timebased policy not found and throwIfNotFound = true</exception>
    public static ImageDeliveryChannel? GetTimebasedChannel(
        this ICollection<ImageDeliveryChannel> imageDeliveryChannels,
        bool throwIfNotFound = false)
        => GetChannel(imageDeliveryChannels, AssetDeliveryChannels.Timebased, "Timebased policy not found",
            throwIfNotFound);
    
    /// <summary>
    /// Get ImageDeliveryChannel record for Image channel, optionally throwing if not found.
    /// </summary>
    /// <param name="imageDeliveryChannels">Collection of <see cref="DeliveryChannelPolicy"/></param>
    /// <param name="throwIfNotFound">If true, and image not found, then exception will the thrown</param>
    /// <returns><see cref="DeliveryChannelPolicy"/>, if found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if image policy not found and throwIfNotFound = true</exception>
    public static ImageDeliveryChannel? GetImageChannel(
        this ICollection<ImageDeliveryChannel> imageDeliveryChannels,
        bool throwIfNotFound = false)
        => GetChannel(imageDeliveryChannels, AssetDeliveryChannels.Image, "Image policy not found",
            throwIfNotFound);
    
    private static ImageDeliveryChannel? GetChannel(
        this ICollection<ImageDeliveryChannel> imageDeliveryChannels,
        string channelName,
        string notFoundMessage,
        bool throwIfNotFound = false)
    {
        if (imageDeliveryChannels.IsNullOrEmpty())
        {
            return throwIfNotFound
                ? throw new InvalidOperationException(notFoundMessage)
                : null;
        }

        return throwIfNotFound
            ? imageDeliveryChannels.Single(dcp => dcp.Channel == channelName)
            : imageDeliveryChannels.SingleOrDefault(dcp => dcp.Channel == channelName);
    }
}
