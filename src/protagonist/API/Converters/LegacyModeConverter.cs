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

        image.DeliveryChannels = GetDeliveryChannelsForLegacyAsset(image);
        
        return image;
    }

    public static DeliveryChannel[]? GetDeliveryChannelsForLegacyAsset<T>(T image)
        where T : Image
    {
        if (image.Family == AssetFamily.Image && MIMEHelper.IsImage(image.MediaType))
        {
            return new DeliveryChannel[]
            {
                new()
                {
                    Channel = AssetDeliveryChannels.Image,
                    Policy = image.ImageOptimisationPolicy == "fast-higher" 
                        ? "default" 
                        : null
                },
                new()
                {
                    Channel = AssetDeliveryChannels.Thumbnails,
                    Policy = image.ThumbnailPolicy == "default"
                        ? "default"
                        : null
                },
            };
        }
        if (image.Family == AssetFamily.Timebased)
        {
            if(MIMEHelper.IsVideo(image.MediaType))
            {
                return new DeliveryChannel[]
                {
                    new()
                    {
                        Channel = AssetDeliveryChannels.Timebased,
                        Policy = image.ImageOptimisationPolicy == "video-max" 
                            ? "default-video"
                            : null
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
                        Policy = image.ImageOptimisationPolicy == "audio-max"
                            ? "default-audio"
                            : null
                    }
                };        
            }    
        }
        if (image.Family == AssetFamily.File)
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

        return Array.Empty<DeliveryChannel>();
    }
}