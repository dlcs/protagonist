using System;
using API.Converters;
using API.Exceptions;
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
        var hydraImage = new Image{ MediaType = "type", Origin = "https://example.org/my-asset",
            MaxUnauthorised = 5, Family = AssetFamily.File };
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.MediaType.Should().Be(hydraImage.MediaType);
        convertedImage.MaxUnauthorised.Should().Be(hydraImage.MaxUnauthorised);
        convertedImage.Family.Should().Be(hydraImage.Family);
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_Fails_WhenOriginNotSpecified()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Family = AssetFamily.Timebased
        };

        // Act
        Action action = () =>
            LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        action.Should()
            .Throw<APIException>()
            .WithMessage("An origin is required when legacy mode is enabled")
            .And.StatusCode.Should().Be(400);
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_SetsMediaType_WithNotSet()
    {
        // Arrange
        var hydraImage = new Image{ MaxUnauthorised = 5, Family = AssetFamily.File,
            Origin = "https://example.org/my-asset"};
        
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
        var hydraImage = new Image{ Origin = "something.jpg",MaxUnauthorised = 5, Family = AssetFamily.File};
        
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
        var hydraImage = new Image{ Origin = "something.jpg",MaxUnauthorised = 0, Family = AssetFamily.File};
        
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
        var hydraImage = new Image{ Id = "https://test/someId", MediaType = "something", 
            Origin = "https://example.org/my-asset" };
        
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
    
    [Theory]
    [InlineData("", "")]
    [InlineData(null, null)]
    [InlineData("http://dlc.io/imageOptimisationPolicies/", "http://dlc.io/thumbnailPolicies/")]
    public void VerifyAndConvertToModernFormat_AddsDeliveryChannels_WhenProvidedWithoutPolicy_ForImage(string iop, string tp)
    {
        // Arrange
        var hydraImage = new Image()
        {
            Family = AssetFamily.Image,
            Origin = "something.jpg",
            ImageOptimisationPolicy = iop,
            ThumbnailPolicy = tp
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

    [Theory]
    [InlineData("mp3")]
    [InlineData("mp4")]
    [InlineData("pdf")]
    public void VerifyAndConvertToModernFormat_TreatsAsImage_ForNonImagesWithoutFamily(string fileExtension)
    {
        // Arrange
        var hydraImage = new Image()
        {
            Origin = $"something.{fileExtension}",
        };

        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Image,
            dc => dc.Channel == AssetDeliveryChannels.Thumbnails);
    }

    [Theory]
    [InlineData("fast-higher")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicies/fast-higher")]
    public void VerifyAndConvertToModernFormat_AddsImageDeliveryChannelsWithPolicies_WhenFastHigherImageOptimisationPolicySpecified(
        string imageOptimisationPolicy)
    {
        // Arrange
        var hydraImage = new Image()
        {
            Origin = "something.jpg",
            ImageOptimisationPolicy = imageOptimisationPolicy
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

    [Theory]
    [InlineData("default")]
    [InlineData("https://api.dlc.services/thumbnailPolicies/default")]
    public void VerifyAndConvertToModernFormat_AddsImageDeliveryChannelsWithPolicies_WhenDefaultThumbnailPolicySpecified(
        string thumbnailPolicy)
    {
        // Arrange
        var hydraImage = new Image()
        {
            Origin = "something.jpg",
            ThumbnailPolicy = thumbnailPolicy
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

    [Theory]
    [InlineData("fast-higher", "default")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicies/fast-higher", 
        "https://api.dlc.services/thumbnailPolicies/default")]
    public void VerifyAndConvertToModernFormat_AddsImageDeliveryChannelsWithPolicies_WithBothPolicyTypesSpecified(
        string imageOptimisationPolicy, string thumbnailPolicy)
    {
        // Arrange
        var hydraImage = new Image()
        {
            Origin = "something.jpg",
            ImageOptimisationPolicy = imageOptimisationPolicy,
            ThumbnailPolicy = thumbnailPolicy
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

    [Theory]
    [InlineData("video-max")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicies/video-max")]
    public void VerifyAndConvertToModernFormat_AddsTimebasedDeliveryChannelWithPolicy_WhenVideoMaxSpecified(
        string imageOptimisationPolicy)
    {
        // Arrange
        var hydraImage = new Image()
        {
            Family = AssetFamily.Timebased,
            Origin = "something.mp4",
            ImageOptimisationPolicy = imageOptimisationPolicy
        };

        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Timebased && 
                  dc.Policy == "default-video");
    }

    [Theory]
    [InlineData("audio-max")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicies/audio-max")]
    public void VerifyAndConvertToModernFormat_AddsTimebasedDeliveryChannelWithPolicy_WhenAudioMaxSpecified(
        string imageOptimisationPolicy)
    {
        // Arrange
        var hydraImage = new Image()
        {
            Family = AssetFamily.Timebased,
            Origin = "something.mp3",
            ImageOptimisationPolicy = imageOptimisationPolicy
        };

        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.DeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Timebased && 
                  dc.Policy == "default-audio");
    }
    
    [Fact]
    public void VerifyAndConvertToModernFormat_Fails_WhenInvalidImageOptimisationPolicySpecified_ForImageAsset()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Origin = "something.tiff",
            ImageOptimisationPolicy = "not-a-policy"
        };

        // Act
        Action action = () =>
            LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        action.Should()
            .Throw<APIException>()
            .WithMessage($"'not-a-policy' is not a valid imageOptimisationPolicy for an image")
            .And.StatusCode.Should().Be(400);
    }

    [Fact]
    public void VerifyAndConvertToModernFormat_Fails_WhenInvalidThumbnailPolicySpecified_ForImageAsset()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Origin = "something.tiff",
            ThumbnailPolicy = "not-a-policy"
        };

        // Act
        Action action = () =>
            LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        action.Should()
            .Throw<APIException>()
            .WithMessage($"'not-a-policy' is not a valid thumbnailPolicy for an image")
            .And.StatusCode.Should().Be(400);
    }
 
    [Fact]
    public void VerifyAndConvertToModernFormat_Fails_WhenImageOptimisationPolicySpecified_ForTimebasedAsset()
    {
        // Arrange
        var hydraImage = new Image()
        {
            Family = AssetFamily.Timebased,
            Origin = "something.mp4",
            ImageOptimisationPolicy= "not-a-policy"
        };

        // Act
        Action action = () =>
            LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        action.Should()
            .Throw<APIException>()
            .WithMessage($"'not-a-policy' is not a valid imageOptimisationPolicy for a timebased asset")
            .And.StatusCode.Should().Be(400);
    }
}
