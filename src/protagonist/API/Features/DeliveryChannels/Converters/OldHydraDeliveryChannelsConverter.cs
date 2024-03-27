using System.Collections.Generic;
using DLCS.HydraModel;
using DLCS.Model.Assets;

namespace API.Features.DeliveryChannels.Converters;

public class OldHydraDeliveryChannelsConverter
{
    public List<DeliveryChannel> Convert(DLCS.HydraModel.Image hydraImage)
    {
        var convertedDeliveryChannels = new List<DeliveryChannel>();
        foreach (var channel in hydraImage.WcDeliveryChannels)
        {
            switch (channel)
            {
                case AssetDeliveryChannels.Image:
                {
                    convertedDeliveryChannels.Add(new DeliveryChannel()
                    {
                        Channel = AssetDeliveryChannels.Image,
                        Policy = hydraImage.ImageOptimisationPolicy == "use-original" 
                            ? "use-original" 
                            : "default"
                    });
                    break;
                }
                case AssetDeliveryChannels.Thumbnails:
                {
                    convertedDeliveryChannels.Add(new DeliveryChannel()
                    {
                        Channel = AssetDeliveryChannels.Thumbnails,
                    });
                    break;
                }
                case AssetDeliveryChannels.Timebased:
                {
                    convertedDeliveryChannels.Add(new DeliveryChannel()
                    {
                        Channel = AssetDeliveryChannels.Timebased,
                    });
                    break;
                }
                case AssetDeliveryChannels.File:
                {
                    convertedDeliveryChannels.Add(new DeliveryChannel()
                    {
                        Channel = AssetDeliveryChannels.File
                    });
                    break;
                }
            }
        }
        return convertedDeliveryChannels;
    }
    public bool CanConvert(DLCS.HydraModel.Image hydraImage)
    {
        if (hydraImage.DeliveryChannels != null)
        {
            return false;
        }
        
        if (hydraImage.WcDeliveryChannels != null ||
            hydraImage.ThumbnailImageService != null ||
            hydraImage.ImageOptimisationPolicy != null)
        {
            return true;
        }

        return false;
    }
}