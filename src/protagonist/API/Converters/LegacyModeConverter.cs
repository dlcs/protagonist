using API.Exceptions;
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
    /// <param name="emulateOldDeliveryChannelProperties">Whether old thumbnailPolicy/imageOptimisationPolicy fields
    /// should be emulated and translated into delivery channels</param>
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

        if (emulateOldDeliveryChannelProperties)
        {
            image.DeliveryChannels = GetDeliveryChannelsForLegacyAsset(image);
        }
      
        return image;
    }

    public static DeliveryChannel[]? GetDeliveryChannelsForLegacyAsset<T>(T image)
        where T : Image
    {
        var imageOptimisationPolicy = image.ImageOptimisationPolicy.GetLastPathElement() ?? image.ImageOptimisationPolicy;
        var thumbnailPolicy = image.ThumbnailPolicy.GetLastPathElement() ?? image.ThumbnailPolicy;
     
        if (image.Family == AssetFamily.Image)
        {
            string? imageChannelPolicy = null;
            if (!imageOptimisationPolicy.IsNullOrEmpty())
            {
                if (imageOptimisationPolicy == "fast-higher")
                {
                    imageChannelPolicy = "default";
                }
                else
                {
                    throw new APIException($"'{imageOptimisationPolicy}' is not a valid imageOptimisationPolicy")
                    {
                        StatusCode = 400
                    };
                }
            }
            
            string? thumbsChannelPolicy = null;
            if (!thumbnailPolicy.IsNullOrEmpty())
            {
                if (thumbnailPolicy == "default")
                {
                    thumbsChannelPolicy = "default";
                }
                else
                {
                    throw new APIException($"'{thumbnailPolicy}' is not a valid thumbnailPolicy")
                    {
                        StatusCode = 400
                    };
                }
            }
            
            return new DeliveryChannel[]
            {
                new()
                {
                    Channel = AssetDeliveryChannels.Image,
                    Policy = imageChannelPolicy
                },
                new()
                {
                    Channel = AssetDeliveryChannels.Thumbnails,
                    Policy = thumbsChannelPolicy
                },
            };
        }
        
        if (image.Family == AssetFamily.Timebased)
        {
            string? avChannelPolicy = null;
            if (!imageOptimisationPolicy.IsNullOrEmpty())
            {
                if (imageOptimisationPolicy == "video-max")
                {
                    avChannelPolicy = "default-video";
                }
                else if (imageOptimisationPolicy == "audio-max")
                {
                    avChannelPolicy = "default-audio";
                }
                else
                {
                    throw new APIException($"'{avChannelPolicy}' is not a valid imageOptimisationPolicy for a timebased asset")
                    {
                        StatusCode = 400
                    };
                }
            }

            return new DeliveryChannel[]
            {
                new()
                {
                    Channel = AssetDeliveryChannels.Timebased,
                    Policy = avChannelPolicy
                }
            };
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