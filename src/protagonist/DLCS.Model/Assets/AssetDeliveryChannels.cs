using System;
using System.Linq;
using DLCS.Core.Collections;

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
    /// Checks if an asset has any delivery channel specified in a list
    /// </summary>
    public static bool HasAnyDeliveryChannel(this Asset asset, params string[] deliveryChannels)
    {
        if (asset.ImageDeliveryChannels.IsNullOrEmpty() || deliveryChannels.IsNullOrEmpty()) return false;
        if (deliveryChannels.Any(dc => !All.Contains(dc)))
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryChannels), deliveryChannels,
                $"Acceptable delivery-channels are: {AllString}");
        }
        
        return asset.ImageDeliveryChannels.Any(dc => deliveryChannels.Contains(dc.Channel));
    }
    
    /// <summary>
    /// Check if asset has specified deliveryChannel
    /// </summary>
    public static bool HasDeliveryChannel(this Asset asset, string deliveryChannel)
        => HasAnyDeliveryChannel(asset, deliveryChannel);
        
    /// <summary>
    /// Checks if asset has specified deliveryChannel only (e.g. 1 channel and it matches specified value
    /// </summary>
    public static bool HasSingleDeliveryChannel(this Asset asset, string deliveryChannel)
        => asset.ImageDeliveryChannels != null &&
           asset.ImageDeliveryChannels.Count == 1 && 
           asset.HasDeliveryChannel(deliveryChannel);
    
    /// <summary>
    /// Checks if asset does not have a specified deliveryChannel
    /// </summary>
    public static bool DoesNotHaveDeliveryChannel(this Asset asset, string deliveryChannel)
        => !asset.HasDeliveryChannel(deliveryChannel);
    
    /// <summary>
    /// Checks if string is a valid delivery channel
    /// </summary>
    public static bool IsValidChannel(string? deliveryChannel)
        => All.Contains(deliveryChannel);

    /// <summary>
    /// Checks if a delivery channel is valid for a given media type
    /// </summary>
    public static bool IsChannelValidForMediaType(string deliveryChannel, string mediaType, bool throwIfChannelUnknown = true) 
        => deliveryChannel switch 
        { 
            Image => mediaType.StartsWith("image/"),
            Thumbnails => mediaType.StartsWith("image/"),
            Timebased => mediaType.StartsWith("video/") || mediaType.StartsWith("audio/"),
            File => true, // A file can be matched to any media type
            None => true, // Likewise for the 'none' channel
            _ when throwIfChannelUnknown => throw new ArgumentOutOfRangeException(nameof(deliveryChannel), deliveryChannel,
                $"Acceptable delivery-channels are: {AllString}"),
            _ => false,
        };
}

