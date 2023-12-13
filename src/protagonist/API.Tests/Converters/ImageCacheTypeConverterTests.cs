using API.Converters;
using DLCS.Model.Assets;

namespace API.Tests.Converters;

public class ImageCacheTypeConverterTests
{
    [Fact]
    public void ImageCacheTypeConverter_ConvertsString_WithCdnAndInternalCache()
    {
        // Arrange
        var imageCacheTypeString = "cdn,internalCache";

        // Act
        var convertedImageCacheFlags = ImageCacheTypeConverter.ConvertToImageCacheType(imageCacheTypeString, ',');
        
        // Assert
        convertedImageCacheFlags.HasFlag(ImageCacheType.Cdn).Should().BeTrue();
        convertedImageCacheFlags.HasFlag(ImageCacheType.InternalCache).Should().BeTrue();
        convertedImageCacheFlags.HasFlag(ImageCacheType.None).Should().BeFalse();
        convertedImageCacheFlags.HasFlag(ImageCacheType.Unknown).Should().BeFalse();
    }
    
    [Fact]
    public void ImageCacheTypeConverter_ReturnsNone_WhenPassedNull()
    {
        // Arrange and Act
        var convertedImageCacheFlags = ImageCacheTypeConverter.ConvertToImageCacheType(null, ',');
        
        // Assert
        convertedImageCacheFlags.HasFlag(ImageCacheType.Cdn).Should().BeFalse();
        convertedImageCacheFlags.HasFlag(ImageCacheType.InternalCache).Should().BeFalse();
        convertedImageCacheFlags.HasFlag(ImageCacheType.None).Should().BeTrue();
        convertedImageCacheFlags.HasFlag(ImageCacheType.Unknown).Should().BeFalse();
    }
}