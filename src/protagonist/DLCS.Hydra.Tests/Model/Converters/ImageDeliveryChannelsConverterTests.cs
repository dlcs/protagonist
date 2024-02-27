using DLCS.HydraModel;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace DLCS.Hydra.Tests.Model.Converters;

public class ImageDeliveryChannelsConverterTests
{
    [Fact]
    public async Task DeliveryChannelsConverter_Supports_Basic_Channels()
    {
        var hydraAssetBody = @"{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/asset.tiff"",
            ""family"": ""I"",
            ""mediaType"": ""image/tiff"",
            ""deliveryChannels"": [""iiif-img"",""thumbs"",""file""]
        }";
        
        var deliveryChannel = JsonConvert.DeserializeObject<Image>(hydraAssetBody);
        
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
    public async Task DeliveryChannelsConverter_Supports_Complex_Channels()
    {
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
              ],
        }";
        
        var deliveryChannel = JsonConvert.DeserializeObject<Image>(hydraAssetBody);
        
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
}