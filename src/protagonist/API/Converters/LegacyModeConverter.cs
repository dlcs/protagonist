using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.HydraModel;
using Hydra;

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
    public static Image VerifyAndConvertToModernFormat(Image image)
    {
        if (image.MediaType.IsNullOrEmpty())
        {
            var contentType = image.Origin?.Split('.').Last() ?? string.Empty;
            
            image.MediaType = MIMEHelper.GetContentTypeForExtension(contentType) ?? DefaultMediaType;
            
            if (image.Origin is not null && image.Family is null && image.DeliveryChannels.IsNullOrEmpty())
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

        return image;
    }
}