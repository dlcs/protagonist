using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.HydraModel;
using DLCS.Model.Assets;
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
    /// <param name="emulateOldDeliveryChannelProperties">Whether old thumbnailPolicy/imageOptimisation behaviour
    /// should be emulated </param>
    /// <returns>A converted image</returns>
    public static T VerifyAndConvertToModernFormat<T>(T image, bool emulateOldDeliveryChannelProperties)
        where T : Image
    {
        if (image.MediaType.IsNullOrEmpty())
        {
            var contentType = image.Origin?.Split('.').Last() ?? string.Empty;
            
            image.MediaType = MIMEHelper.GetContentTypeForExtension(contentType) ?? DefaultMediaType;
            
            if (image.Origin is not null && image.Family is null)
            {
                  image.Family = DLCS.HydraModel.AssetFamily.Image;
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

        if (emulateOldDeliveryChannelProperties)
        {
            image.DeliveryChannels = GetDeliveryChannelsForLegacyAsset(image);
        }
      
        return image;
    }

    public static DeliveryChannel[]? GetDeliveryChannelsForLegacyAsset<T>(T image)
        where T : Image
    {
        var thumbnailPolicy = image.ThumbnailPolicy.GetLastPathElement() ?? image.ThumbnailPolicy;
        var imageOptimisationPolicy = image.ImageOptimisationPolicy.GetLastPathElement() ?? image.ImageOptimisationPolicy;
        
        if (image.Family == AssetFamily.Image)
        {
            return new DeliveryChannel[]
            {
                new()
                {
                    Channel = AssetDeliveryChannels.Image,
                    Policy = imageOptimisationPolicy == "fast-higher" 
                        ? "default" 
                        : null
                },
                new()
                {
                    Channel = AssetDeliveryChannels.Thumbnails,
                    Policy = thumbnailPolicy == "default"
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
                        Policy = imageOptimisationPolicy == "video-max" 
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
                        Policy = imageOptimisationPolicy == "audio-max"
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