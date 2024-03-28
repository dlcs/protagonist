using System.Collections.Generic;
using API.Features.DeliveryChannels.Converters;
using DLCS.HydraModel;

namespace API.Tests.Features.DeliveryChannels.Converters;

public class OldHydraDeliveryChannelsConverterTests
{ 
    private readonly OldHydraDeliveryChannelsConverter sut; 
    
    public OldHydraDeliveryChannelsConverterTests()
    {
        sut = new OldHydraDeliveryChannelsConverter();
    }

    [Fact]
    public void CanConvert_ReturnsFalse_IfImageUsesNewDeliveryChannels()
    {
        // Arrange
        var image = new Image()
        {
            DeliveryChannels = new DeliveryChannel[]
            {
                new()
                {
                    Channel = "iiif-img",
                    Policy = "my-iiif-img-policy"
                },
                new()
                {
                    Channel = "thumbs",
                    Policy = "my-thumbs-policy"
                }
            }
        };

        // Act
        var result = sut.CanConvert(image);
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void CanConvert_ReturnsTrue_IfImageUsesOldDeliveryChannels()
    {
        // Arrange
        var image = new Image()
        {
            WcDeliveryChannels = new[]{"file"},
        };

        // Act
        var result = sut.CanConvert(image);
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void Convert_TranslatesImageChannel()
    {
        // Arrange
        var image = new Image()
        {
            WcDeliveryChannels = new[]{"iiif-img"},
        };
        
        // Act
        var result = sut.Convert(image);
        
        // Assert
        result.Should().BeEquivalentTo(new List<DeliveryChannel>()
        {
            new()
            {
                Channel = "iiif-img",
                Policy = "default"
            }
        });
    }
    
    [Fact]
    public void Convert_TranslatesImageChannel_WithUseOriginalPolicy()
    {
        // Arrange
        var image = new Image()
        {
            WcDeliveryChannels = new[]{"iiif-img"},
            ImageOptimisationPolicy = "use-original"
        };
        
        // Act
        var result = sut.Convert(image);
        
        // Assert
        result.Should().BeEquivalentTo(new List<DeliveryChannel>()
        {
            new()
            {
                Channel = "iiif-img",
                Policy = "use-original"
            }
        });
    }
    
    [Fact]
    public void Convert_TranslatesAvChannel()
    {
        // Arrange
        var image = new Image()
        {
            WcDeliveryChannels = new[]{"iiif-av"}
        };
        
        // Act
        var result = sut.Convert(image);
        
        // Assert
        result.Should().BeEquivalentTo(new List<DeliveryChannel>()
        {
            new()
            {
                Channel = "iiif-av"
            }
        });
    }
    
    [Fact]
    public void Convert_TranslatesFileChannel()
    {
        // Arrange
        var image = new Image()
        {
            WcDeliveryChannels = new[]{"file"}
        };
        
        // Act
        var result = sut.Convert(image);
        
        // Assert
        result.Should().BeEquivalentTo(new List<DeliveryChannel>()
        {
            new()
            {
                Channel = "file",
                Policy = "none"
            }
        });
    }
}