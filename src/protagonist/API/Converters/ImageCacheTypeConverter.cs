using DLCS.Core.Collections;
using DLCS.Core.Enum;
using DLCS.Model.Assets;

namespace API.Converters;

public static class ImageCacheTypeConverter
{
    public static ImageCacheType ConvertToImageCacheType(string? imageCache, char separator)
    {
        if (imageCache.IsNullOrEmpty())
        {
            return ImageCacheType.None;
        }

        ImageCacheType? imageCacheType = null;

        foreach (var imageCacheValue in imageCache.Split(separator))
        {
            var convertedImageCacheType = imageCacheValue.GetEnumFromString<ImageCacheType>();

            if (imageCacheType == null)
            {
                imageCacheType = convertedImageCacheType;
            }
            else
            {
                imageCacheType |= convertedImageCacheType;
            }
        }

        return imageCacheType ?? ImageCacheType.None;
    }
}