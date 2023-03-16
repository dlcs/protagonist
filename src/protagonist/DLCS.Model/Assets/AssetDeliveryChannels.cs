using System;
using System.Linq;
using DLCS.Core.Collections;

namespace DLCS.Model.Assets;

public static class AssetDeliveryChannels
{
    public const string Image = "iiif-img";
    public const string Timebased = "iiif-av";
    public const string File = "file";
    public const string Thumbs = "thumbs";

    /// <summary>
    /// All possible delivery channels
    /// </summary>
    public static string[] All { get; } = { File, Timebased, Image, Thumbs };

    /// <summary>
    /// All possible delivery channels as a comma-delimited string
    /// </summary>
    public static readonly string AllString = string.Join(',', All);

    /// <summary>
    /// Check if asset has specified deliveryChannel
    /// </summary>
    public static bool HasDeliveryChannel(this Asset asset, string deliveryChannel)
    {
        if (asset.DeliveryChannel.IsNullOrEmpty()) return false;
        if (!All.Contains(deliveryChannel))
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryChannel), deliveryChannel,
                $"Acceptable delivery-channels are: {AllString}");
        }

        return asset.DeliveryChannel.Contains(deliveryChannel);
    }
    
    /// <summary>
    /// Checks if asset has specified deliveryChannel only (e.g. 1 channel and it matches specified value
    /// </summary>
    public static bool HasSingleDeliveryChannel(this Asset asset, string deliveryChannel) 
        => asset.HasDeliveryChannel(deliveryChannel) && asset.DeliveryChannel.Length == 1;
}