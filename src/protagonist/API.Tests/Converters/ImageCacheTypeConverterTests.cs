using API.Converters;
using DLCS.Model.Assets;

namespace API.Tests.Converters;

public class ImageCacheTypeConverterTests
{
    [Theory]
    [InlineData("cdn,internalCache")]
    [InlineData("internalCache,cdn")]
    [InlineData("cdn,,internalCache")]
    [InlineData("cdn, internalCache")]
    [InlineData("cdn ,internalCache")]
    public void ImageCacheTypeConverter_ConvertsString_WithCdnAndInternalCache(string imageCacheTypeString)
    {
        // Act
        var convertedImageCacheFlags = ImageCacheTypeConverter.ConvertToImageCacheType(imageCacheTypeString, ',');
        
        // Assert
        convertedImageCacheFlags.Should().HaveFlag(ImageCacheType.Cdn)
            .And.HaveFlag(ImageCacheType.InternalCache)
            .And.NotHaveFlag(ImageCacheType.None)
            .And.NotHaveFlag(ImageCacheType.Unknown);
    }
    
    [Fact]
    public void ImageCacheTypeConverter_ReturnsNone_WhenPassedNull()
    {
        // Arrange and Act
        var convertedImageCacheFlags = ImageCacheTypeConverter.ConvertToImageCacheType(null, ',');
        
        // Assert
        convertedImageCacheFlags.Should().NotHaveFlag(ImageCacheType.Cdn)
            .And.NotHaveFlag(ImageCacheType.InternalCache)
            .And.HaveFlag(ImageCacheType.None)
            .And.NotHaveFlag(ImageCacheType.Unknown);
    }
}
