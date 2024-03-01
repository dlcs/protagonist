using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using IIIF.Presentation.V3.Content;

namespace DLCS.Model.Assets;

public static class AssetDeliveryChannels
{
    public const string Image = "iiif-img";
    public const string Thumbnails = "thumbs";
    public const string Timebased = "iiif-av";
    public const string File = "file";
    public const string None = "none";

    /// <summary>
    /// All possible delivery channels
    /// </summary>
    public static string[] All { get; } = { File, Timebased, Image, Thumbnails, None };

    /// <summary>
    /// All possible delivery channels as a comma-delimited string
    /// </summary>
    public static readonly string AllString = string.Join(',', All);

    /// <summary>
    /// Check if asset has specified deliveryChannel
    /// </summary>
    public static bool HasDeliveryChannel(this Asset asset, string deliveryChannel)
    {
        if (asset.DeliveryChannels.IsNullOrEmpty()) return false;
        if (!All.Contains(deliveryChannel))
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryChannel), deliveryChannel,
                $"Acceptable delivery-channels are: {AllString}");
        }

        return asset.DeliveryChannels.Contains(deliveryChannel);
    }
    
    /// <summary>
    /// Checks if asset has specified deliveryChannel only (e.g. 1 channel and it matches specified value
    /// </summary>
    public static bool HasSingleDeliveryChannel(this Asset asset, string deliveryChannel) 
        => asset.DeliveryChannels.ContainsOnly(deliveryChannel);
    
    /// <summary>
    /// Checks if string is a valid delivery channel
    /// </summary>
    public static bool IsValidChannel(string? deliveryChannel)
        => All.Contains(deliveryChannel);

    /// <summary>
    /// Checks if a delivery channel is valid for a given media type
    /// </summary>
    public static bool IsChannelValidForMediaType(string deliveryChannel, string mediaType) 
        => deliveryChannel switch 
        { 
            Image => mediaType.StartsWith("image/"),
            Thumbnails => mediaType.StartsWith("image/"),
            Timebased => mediaType.StartsWith("video/") || mediaType.StartsWith("audio/"),
            File => true, // A file can be matched to any media type
            None => true, // Likewise for the 'none' channel
            _ => throw new ArgumentOutOfRangeException(nameof(deliveryChannel), deliveryChannel,
                $"Acceptable delivery-channels are: {AllString}")
        };
}

