using API.Converters;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using AssetFamily = DLCS.HydraModel.AssetFamily;

namespace API.Tests.Converters;

public class LegacyModeConverterTests
{
    [Fact]
    public void VerifyAndConvertToModernFormat_ChangesNothing_WithNewFormat()
    {
        // Arrange
        var hydraImage = new Image{ MediaType = "type", MaxUnauthorised = 5, Family = AssetFamily.File};
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.MediaType.Should().Be(hydraImage.MediaType);
        convertedImage.MaxUnauthorised.Should().Be(hydraImage.MaxUnauthorised);
        convertedImage.Family.Should().Be(hydraImage.Family);
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_SetsMediaType_WithNotSet()
    {
        // Arrange
        var hydraImage = new Image{ MaxUnauthorised = 5, Family = AssetFamily.File};
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.MediaType.Should().Be("image/unknown");
        convertedImage.MaxUnauthorised.Should().Be(hydraImage.MaxUnauthorised);
        convertedImage.Family.Should().Be(hydraImage.Family);
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_InferMediaType_WhenOriginSet()
    {
        // Arrange
        var hydraImage = new Image{ Origin = "something.jpg", MaxUnauthorised = 5, Family = AssetFamily.File};
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.MediaType.Should().Be("image/jpeg");
        convertedImage.MaxUnauthorised.Should().Be(hydraImage.MaxUnauthorised);
        convertedImage.Family.Should().Be(hydraImage.Family);
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_SetMaxUnauthorised_WhenSetToOldFormat()
    {
        // Arrange
        var hydraImage = new Image{ Origin = "something.jpg", MaxUnauthorised = 0, Family = AssetFamily.File};
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.MediaType.Should().Be("image/jpeg");
        convertedImage.MaxUnauthorised.Should().Be(-1);
        convertedImage.Family.Should().Be(hydraImage.Family);
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_SetFamily_WhenNotSet()
    {
        // Arrange
        var hydraImage = new Image{ Origin = "something.jpg"};
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.MediaType.Should().Be("image/jpeg");
        convertedImage.MaxUnauthorised.Should().Be(-1);
        convertedImage.Family.Should().Be(AssetFamily.Image);
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_MaxUnauthorisedUnchanged_WhenRolesSet()
    {
        // Arrange
        var hydraImage = new Image{ Origin = "something.jpg", Roles = new []{ "some role" }};
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.MediaType.Should().Be("image/jpeg");
        convertedImage.MaxUnauthorised.Should().Be(null);
        convertedImage.Family.Should().Be(AssetFamily.Image);
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_ModelIdSet_WhenNoModelId()
    {
        // Arrange
        var hydraImage = new Image{ Id = "https://test/someId", MediaType = "something"};
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.ModelId.Should().Be("someId");
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_AddsDeliveryChannels_WhenNotSet_ForImage()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Family = AssetFamily.Image,
            Origin = "something.jpg",
        };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);
        
        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Image && 
                  dc.Policy == null,
            dc => dc.Channel == AssetDeliveryChannels.Thumbnails &&
                  dc.Policy == null);
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_AddsDeliveryChannels_WhenNotSet_ForVideo()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Family = AssetFamily.Timebased,
            Origin = "something.mp4",
        };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);
        
        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Timebased &&
                  dc.Policy == null);
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_AddsDeliveryChannels_WhenNotSet_ForAudio()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Family = AssetFamily.Timebased,
            Origin = "something.mp3",
        };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);
        
        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Timebased && 
                  dc.Policy == null);
    }
        
    [Theory]
    [InlineData("mp3")]
    [InlineData("mp4")]
    public void VerifyAndConvertToModernFormat_AddsNoDeliveryChannels_ForTimebasedMedia_WhenNoFamilySpecified(string fileExtension)
    {
        // Arrange
        var hydraImage = new Image()
        {
            Origin = $"something.{fileExtension}",
        };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);
        
        // Assert
        convertedImage.DeliveryChannels.Should().BeEmpty();
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_AddsDeliveryChannels_WhenNotSet_ForFile()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Family = AssetFamily.File,
            Origin = "something.pdf",
        };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);
        
        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.File && 
                  dc.Policy == "none");
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_AddsNoDeliveryChannels_ForFile_WhenNoFamilySpecified()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Origin = "something.pdf",
        };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);
        
        // Assert
        convertedImage.DeliveryChannels.Should().BeEmpty();
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_AddsImageDeliveryChannelsWithPolicies_WhenFastHigherImageOptimisationPolicySpecified()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Origin = "something.jpg",
            ImageOptimisationPolicy = "fast-higher"
        };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);
        
        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Image && 
                  dc.Policy == "default",
            dc => dc.Channel == AssetDeliveryChannels.Thumbnails && 
                  dc.Policy == null);
    }
        
    [Fact]
    public void VerifyAndConvertToModernFormat_AddsImageDeliveryChannelsWithPolicies_WhenDefaultThumbnailPolicySpecified()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Origin = "something.jpg",
            ThumbnailPolicy = "default"
        };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);
        
        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Image && 
                  dc.Policy == null,
            dc => dc.Channel == AssetDeliveryChannels.Thumbnails && 
                  dc.Policy == "default");
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_AddsImageDeliveryChannelsWithPolicies_WithBothPolicyTypesSpecified()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Origin = "something.jpg",
            ImageOptimisationPolicy = "fast-higher",
            ThumbnailPolicy = "default"
        };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);
        
        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Image && 
                  dc.Policy == "default",
            dc => dc.Channel == AssetDeliveryChannels.Thumbnails && 
                  dc.Policy == "default");
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_AddsTimebasedDeliveryChannelWithPolicy_WhenVideoMaxSpecified()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Family = AssetFamily.Timebased,
            Origin = "something.mp4",
            ImageOptimisationPolicy = "video-max"
        };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);
        
        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Timebased && 
                  dc.Policy == "default-video");
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_AddsTimebasedDeliveryChannelWithPolicy_WhenAudioMaxSpecified()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Family = AssetFamily.Timebased,
            Origin = "something.mp3",
            ImageOptimisationPolicy = "audio-max"
        };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);
        
        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Timebased && 
                  dc.Policy == "default-audio");
    }
}