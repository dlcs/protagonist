using System.Collections.Generic;
using DLCS.Core.Collections;
using DLCS.HydraModel;
using DLCS.Model.Assets;

namespace API.Features.DeliveryChannels.Converters;

public class OldHydraDeliveryChannelsConverter
{
    private const string ImageDefaultPolicy = "default";
    private const string ImageUseOriginalPolicy = "use-original";
    private const string FileNonePolicy = "none";
    
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
                AssetDeliveryChannels.Thumbnails or
                AssetDeliveryChannels.Timebased or
                AssetDeliveryChannels.File => new DeliveryChannel()
                {
                    Channel = wcDeliveryChannel,
                    Policy = FileNonePolicy
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

    public bool CanConvert(DLCS.HydraModel.Image hydraImage)
        => hydraImage.WcDeliveryChannels != null;
}