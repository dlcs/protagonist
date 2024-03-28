using System.Collections.Generic;
using DLCS.Core.Collections;
using DLCS.HydraModel;
using DLCS.Model.Assets;

namespace API.Features.DeliveryChannels.Converters;

/// <summary>
/// Conversion between asset.WcDeliveryChannels and asset.DeliveryChannels
/// </summary>
public class OldHydraDeliveryChannelsConverter
{
    private const string ImageDefaultPolicy = "default";
    private const string ImageUseOriginalPolicy = "use-original";
    private const string FileNonePolicy = "none";
    
    /// <summary>
    /// Convert an asset's WcDeliveryChannels into a list of HydraModel.DeliveryChannel objects
    /// </summary>
    public DeliveryChannel[]? Convert(DLCS.HydraModel.Image hydraImage)
    {
        if (hydraImage.WcDeliveryChannels.IsNullOrEmpty()) return null;
        
        var convertedDeliveryChannels = new List<DeliveryChannel>();
        
        foreach (var wcDeliveryChannel in hydraImage.WcDeliveryChannels)
        {
            var matchedDeliveryChannel = wcDeliveryChannel switch
            {
                AssetDeliveryChannels.Image => new DeliveryChannel()
                {
                    Channel = AssetDeliveryChannels.Image,
                    Policy = hydraImage.ImageOptimisationPolicy == ImageUseOriginalPolicy
                        ? ImageUseOriginalPolicy
                        : ImageDefaultPolicy
                },
                AssetDeliveryChannels.File => new DeliveryChannel()
                {
                    Channel = AssetDeliveryChannels.File,
                    Policy = FileNonePolicy
                },
                AssetDeliveryChannels.Thumbnails or
                AssetDeliveryChannels.Timebased => new DeliveryChannel()
                {
                    Channel = wcDeliveryChannel,
                },
                _ => null
            };
            
            if (matchedDeliveryChannel != null)
            {
                convertedDeliveryChannels.Add(matchedDeliveryChannel);
            }
        }
        
        return convertedDeliveryChannels.ToArray();
    }
}