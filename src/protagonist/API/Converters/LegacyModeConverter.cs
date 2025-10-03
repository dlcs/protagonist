using API.Exceptions;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using Hydra;
using Microsoft.Extensions.Logging;
using AssetFamily = DLCS.HydraModel.AssetFamily;

namespace API.Converters;

/// <summary>
/// Converts legacy image API calls
/// </summary>
public static class LegacyModeConverter
{
    internal static void LogLegacyUsage(this ILogger logger, string message, params object?[] args)
        => logger.LogWarning("LEGACY USE:" + message, args);
    
    /// <summary>
    /// Converts from legacy format to new format
    /// </summary>
    /// <param name="image">The image to convert should be emulated and translated into delivery channels</param>
    /// <returns>A converted image</returns>
    public static T VerifyAndConvertToModernFormat<T>(T image, ILogger? logger = null)
        where T : Image
    {
        if (image.Origin.IsNullOrEmpty())
        {
            throw new BadRequestException("An origin is required when legacy mode is enabled");  
        }
        
        if (image.MediaType.IsNullOrEmpty())
        {
            logger?.LogLegacyUsage("Null or empty media type");
            var contentType = image.Origin?.Split('.').Last() ?? string.Empty;
         
            image.MediaType = MIMEHelper.GetContentTypeForExtension(contentType) ?? MIMEHelper.UnknownImage;
            image.Family ??= AssetFamily.Image;
        }
        
        if (image.ModelId is null && image.Id is not null)
        {
            image.ModelId = image.Id.GetLastPathElement();
        }

        if (image.MaxUnauthorised is null or 0 && image.Roles.IsNullOrEmpty())
        {
            logger?.LogLegacyUsage("MaxUnauthorised");
            image.MaxUnauthorised = -1;
        }
        
        image.DeliveryChannels = GetDeliveryChannelsForLegacyAsset(image);
        
        // Clear IOP and TP after we've matched them to the appropriate delivery channels
        image.ImageOptimisationPolicy = null;
        image.ThumbnailPolicy = null;
        
        return image;
    }

    private static DeliveryChannel[]? GetDeliveryChannelsForLegacyAsset<T>(T image)
        where T : Image
    {
        // Retrieve the name, if it is a path to a DLCS IOP/TP policy resource
        var imageOptimisationPolicy = GetPolicyValue(image.ImageOptimisationPolicy, "imageOptimisationPolicies/");
        var thumbnailPolicy = GetPolicyValue(image.ThumbnailPolicy, "thumbnailPolicies/");
        
        // If IOP/TP specified, try to map given value. Else fallback to configured defaults (by returning null policy)
        if (image.Family == AssetFamily.Image || (image.Family == null && MIMEHelper.IsImage(image.MediaType)))
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
                    throw new BadRequestException($"'{imageOptimisationPolicy}' is not a valid imageOptimisationPolicy for an image");
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
                    throw new BadRequestException($"'{thumbnailPolicy}' is not a valid thumbnailPolicy for an image");
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
                    throw new BadRequestException(
                        $"'{imageOptimisationPolicy}' is not a valid imageOptimisationPolicy for a timebased asset");
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
        
        return null;
    }
    
    private static string? GetPolicyValue(string? policyValue, string pathSlug)
    {
        if (string.IsNullOrEmpty(policyValue)) return policyValue;
        
        var candidate = policyValue.GetLastPathElement(pathSlug);
        if (!string.IsNullOrEmpty(candidate)) return candidate;
        
        // This catches sending payloads that only contain the initial slug, without a value
        // e.g. https://dlcs.io/thumbnailPolicies/
        return policyValue.EndsWith(pathSlug) ? null : policyValue;
    }
}
