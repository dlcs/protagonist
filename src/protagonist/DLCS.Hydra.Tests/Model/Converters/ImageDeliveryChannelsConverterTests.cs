﻿using DLCS.HydraModel;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace DLCS.Hydra.Tests.Model.Converters;

public class ImageDeliveryChannelsConverterTests
{
    [Fact]
    public void DeliveryChannelsConverter_Supports_Basic_Channels()
    {
        // Arrange
        var hydraAssetBody = @"{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/asset.tiff"",
            ""family"": ""I"",
            ""mediaType"": ""image/tiff"",
            ""deliveryChannels"": [""iiif-img"",""thumbs"",""file""]
        }";
        
        // Act
        var deliveryChannel = JsonConvert.DeserializeObject<Image>(hydraAssetBody);
        
        // Assert
        deliveryChannel!.DeliveryChannels!.Length.Should().Be(3);
        deliveryChannel!.DeliveryChannels!.Should().BeEquivalentTo(new DeliveryChannel[]
        {
            new()
            {
                Channel = "iiif-img",
                Policy = null,
            },
            new()
            {
                Channel = "thumbs",
                Policy = null,
            },
            new()
            {
                Channel = "file",
                Policy = null,
            }
        });
    }
    
    [Fact]
    public void DeliveryChannelsConverter_Supports_Complex_Channels()
    {
        // Arrange
        var hydraAssetBody = @"{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/asset.tiff"",
          ""family"": ""I"",
          ""mediaType"": ""image/tiff"",
            ""deliveryChannels"": [
                {
                  ""channel"": ""iiif-img"",
                  ""policy"": ""default""
                },
                {
                  ""channel"": ""thumbs"",
                  ""policy"": ""my-thumbs-policy""   
                },
                {
                  ""channel"": ""file"",
                  ""policy"": ""none""
                }
              ],
        }";
        
        // Act
        var deliveryChannel = JsonConvert.DeserializeObject<Image>(hydraAssetBody);
        
        // Assert
        deliveryChannel!.DeliveryChannels!.Length.Should().Be(3);
        deliveryChannel!.DeliveryChannels!.Should().BeEquivalentTo(new DeliveryChannel[]
        {
            new()
            {
                Channel = "iiif-img",
                Policy = "default"
            },
            new()
            {
                Channel = "thumbs",
                Policy = "my-thumbs-policy"
            },
            new()
            {
                Channel = "file",
                Policy = "none"
            }
        });
    }
    
    [Fact]
    public void DeliveryChannelsConverter_Supports_Mixed_Channels()
    {
        // Arrange
        var hydraAssetBody = @"{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/asset.tiff"",
          ""family"": ""I"",
          ""mediaType"": ""image/tiff"",
            ""deliveryChannels"": [
                ""iiif-img"",
                {
                  ""channel"": ""thumbs"",
                  ""policy"": ""my-thumbs-policy""   
                },
                {
                  ""channel"": ""file"",
                  ""policy"": ""none""
                }
              ]
        }";
        
        // Act
        var deliveryChannel = JsonConvert.DeserializeObject<Image>(hydraAssetBody);
        
        // Assert
        deliveryChannel!.DeliveryChannels!.Length.Should().Be(3);
        deliveryChannel!.DeliveryChannels!.Should().BeEquivalentTo(new DeliveryChannel[]
        {
            new()
            {
                Channel = "iiif-img",
                Policy = null
            },
            new()
            {
                Channel = "thumbs",
                Policy = "my-thumbs-policy"
            },
            new()
            {
                Channel = "file",
                Policy = "none"
            }
        });
    }
    
    [Fact]
    public void DeliveryChannelsConverter_ReturnsNull_When_Null()
    {
        // Arrange
        var hydraAssetBody = @"{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/asset.tiff"",
            ""family"": ""I"",
            ""mediaType"": ""image/tiff"",
        }";
        
        // Act
        var deliveryChannel = JsonConvert.DeserializeObject<Image>(hydraAssetBody);

        // Assert
        deliveryChannel!.DeliveryChannels!.Should().BeNull();
    }
    
    [Fact]
    public void DeliveryChannelsConverter_ReturnsEmptyArray_When_Empty()
    {
        // Arrange
        var hydraAssetBody = @"{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/asset.tiff"",
            ""family"": ""I"",
            ""mediaType"": ""image/tiff"",
            ""deliveryChannels"": """"
        }";
        
        // Act
        var deliveryChannel = JsonConvert.DeserializeObject<Image>(hydraAssetBody);

        // Assert
        deliveryChannel!.DeliveryChannels!.Should().BeEmpty();
    }
}