using System.Collections.Generic;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using Hydra;
using AssetFamily = DLCS.HydraModel.AssetFamily;

namespace API.Converters;

/// <summary>
/// Converts legacy image API calls
/// </summary>
public static class LegacyModeConverter
{
    private const string DefaultMediaType = "image/unknown";
    
    /// <summary>
    /// Converts from legacy format to new format
    /// </summary>
    /// <param name="image">The image to convert</param>
    /// <returns>A converted image</returns>
    public static T VerifyAndConvertToModernFormat<T>(T image)
        where T : Image
    {
        if (image.MediaType.IsNullOrEmpty())
        {
            var contentType = image.Origin?.Split('.').Last() ?? string.Empty;
            
            image.MediaType = MIMEHelper.GetContentTypeForExtension(contentType) ?? DefaultMediaType;
            
            if (image.Origin is not null && image.Family is null && image.WcDeliveryChannels.IsNullOrEmpty())
            {
                  image.Family = AssetFamily.Image;
            }
        }
        
        if (image.ModelId is null && image.Id is not null)
        {
            image.ModelId = image.Id.GetLastPathElement();
        }

        if (image.MaxUnauthorised is null or 0 && image.Roles.IsNullOrEmpty())
        {
            image.MaxUnauthorised = -1;
        }

        image.DeliveryChannels = GetDeliveryChannelsForMediaType(image);
        
        return image;
    }

    public static DeliveryChannel[]? GetDeliveryChannelsForMediaType<T>(T image)
        where T : Image
    {
        var deliveryChannels = new List<DeliveryChannel>();
        
        if (!image.ImageOptimisationPolicy.IsNullOrEmpty() && image.ImageOptimisationPolicy == "fast-higher")
        {
            deliveryChannels.Add(new DeliveryChannel()
            {
                Channel = AssetDeliveryChannels.Image,
                Policy = "default"
            });
        }

        if (!image.ThumbnailPolicy.IsNullOrEmpty() && image.ThumbnailPolicy == "default")
        {
            deliveryChannels.Add(new DeliveryChannel()
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                Policy = "default"
            });
        }
        
        if (image.ImageOptimisationPolicy.IsNullOrEmpty() && image.ThumbnailPolicy.IsNullOrEmpty() && 
            image.DeliveryChannels.IsNullOrEmpty())
        {
            if (MIMEHelper.IsImage(image.MediaType))
            {
                return new DeliveryChannel[]
                {
                    new()
                    {
                        Channel = AssetDeliveryChannels.Image,
                        Policy = "default"
                    },
                    new()
                    {
                        Channel = AssetDeliveryChannels.Thumbnails,
                        Policy = "default"
                    },
                };
            }
            if (MIMEHelper.IsVideo(image.MediaType))
            {
                return new DeliveryChannel[]
                {
                    new()
                    {
                        Channel = AssetDeliveryChannels.Timebased,
                        Policy = "default-video"
                    }
                };       
            }
            if (MIMEHelper.IsAudio(image.MediaType))
            {
                return new DeliveryChannel[]
                {
                    new()
                    {
                        Channel = AssetDeliveryChannels.Timebased,
                        Policy = "default-audio"
                    }
                };        
            }
            if (MIMEHelper.IsApplication(image.MediaType))
            {
                return new DeliveryChannel[]
                {
                    new()
                    {
                        Channel = AssetDeliveryChannels.File,
                        Policy = "none"
                    }       
                };
            }
        }
        
        return deliveryChannels.ToArray();
    }
}