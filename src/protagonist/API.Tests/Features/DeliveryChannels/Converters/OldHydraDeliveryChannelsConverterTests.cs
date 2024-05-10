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
                Policy = "default",
            },
            new()
            {
                Channel = "thumbs",
                Policy = null,
            },
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
            },
            new()
            {
                Channel = "thumbs",
                Policy = null,
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
                Channel = "iiif-av",
                Policy = null,
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
    
    [Fact]
    public void Convert_TranslatesMultipleChannels()
    {
        // Arrange
        var image = new Image()
        {
            WcDeliveryChannels = new[]{"iiif-img","thumbs","file"}
        };
        
        // Act
        var result = sut.Convert(image);
        
        // Assert
        result.Should().BeEquivalentTo(new List<DeliveryChannel>()
        { 
            new()
            {
                Channel = "iiif-img",
                Policy = "default",
            },
            new()
            {
                Channel = "thumbs",
                Policy = null,
            },
            new()
            {
                Channel = "file",
                Policy = "none"
            }
        });
    }
}