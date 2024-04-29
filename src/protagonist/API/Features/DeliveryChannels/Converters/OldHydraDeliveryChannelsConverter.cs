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
            var matchedDeliveryChannels = wcDeliveryChannel switch
            {
                AssetDeliveryChannels.Image => new List<DeliveryChannel>(){ 
                    new() 
                    {
                        Channel = AssetDeliveryChannels.Image,
                        Policy = hydraImage.ImageOptimisationPolicy == ImageUseOriginalPolicy
                            ? ImageUseOriginalPolicy
                            : ImageDefaultPolicy 
                    },
                    new() 
                    {
                        Channel = AssetDeliveryChannels.Thumbnails
                    },
                },
                AssetDeliveryChannels.File => new List<DeliveryChannel>()
                {
                    new()
                    {
                        Channel = AssetDeliveryChannels.File,
                        Policy = FileNonePolicy
                    }
                },
                AssetDeliveryChannels.Timebased => new List<DeliveryChannel>()
                { 
                    new()
                    {
                        Channel = wcDeliveryChannel,
                    }
                },
                _ => null
            };
            
            if (matchedDeliveryChannels != null)
            {
                convertedDeliveryChannels.AddRange(matchedDeliveryChannels);
            }
        }
        
        return convertedDeliveryChannels.ToArray();
    }
}