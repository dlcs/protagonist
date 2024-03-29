﻿using API.Converters;
using DLCS.HydraModel;

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
        var hydraImage = new Image{ Id = "https://test/someId", MediaType = "something"};
        
        // Act
        var convertedImage = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraImage);

        // Assert
        convertedImage.ModelId.Should().Be("someId");
    }
}