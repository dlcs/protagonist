using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Policies;
using Engine.Ingest;
using Engine.Ingest.Image.ImageServer;
using Engine.Ingest.Persistence;

namespace Engine.Tests.Ingest.Image.Appetiser;

public class ImageProcessorFlagsTests
{
    [Fact]
    public void Ctor_Throws_IfAssetFromOriginNull()
    {
        // Arrange
        var context = new IngestionContext(new Asset());
        
        // Act
        Action action = () => new ImageServerClient.ImageProcessorFlags(context, "");
        
        // Asset
        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    public void Ctor_DoNotUseOriginal_NotOptimised(string mediaType)
    {
        // Arrange
        var context = GetContext(false, false, mediaType);
        
        // Act
        var flags = new ImageServerClient.ImageProcessorFlags(context, "/path/to/generated.jp2");
        
        // Asset
        flags.IsTransient.Should().BeFalse();
        flags.AlreadyUploaded.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeTrue();
        flags.ImageServerFilePath.Should().Be("/path/to/generated.jp2");
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    public void Ctor_DoNotUseOriginal_Optimised(string mediaType)
    {
        // Arrange
        var context = GetContext(false, true, mediaType);
        
        // Act
        var flags = new ImageServerClient.ImageProcessorFlags(context, "/path/to/generated.jp2");
        
        // Asset
        flags.IsTransient.Should().BeFalse();
        flags.AlreadyUploaded.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeTrue();
        flags.ImageServerFilePath.Should().Be("/path/to/generated.jp2");
    }

    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    [InlineData("image/jpeg")]
    public void Ctor_UseOriginalJP2_NotOptimised(string mediaType)
    {
        // Arrange
        var context = GetContext(true, false, mediaType);
        
        // Act
        var flags = new ImageServerClient.ImageProcessorFlags(context, "/path/to/generated.jp2");
        
        // Asset
        flags.IsTransient.Should().BeFalse();
        flags.AlreadyUploaded.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeTrue();
        flags.SaveInDlcsStorage.Should().BeTrue();
        flags.ImageServerFilePath.Should().Be("/path/to/original");
    }

    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    [InlineData("image/jpeg")]
    public void Ctor_UseOriginalNotJP2_Optimised(string mediaType)
    {
        // Arrange
        var context = GetContext(true, true, mediaType);
        
        // Act
        var flags = new ImageServerClient.ImageProcessorFlags(context, "/path/to/generated.jp2");
        
        // Asset
        flags.IsTransient.Should().BeFalse();
        flags.AlreadyUploaded.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeTrue();
        flags.SaveInDlcsStorage.Should().BeFalse();
        flags.ImageServerFilePath.Should().Be("/path/to/original");
    }
    
    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    [InlineData("image/jpeg")]
    public void Ctor_UseOriginalJP2_Optimised_NoImageChannel(string mediaType)
    {
        // Arrange
        var context = GetContext(true, true, mediaType, false);
        
        // Act
        var flags = new ImageServerClient.ImageProcessorFlags(context, "/path/to/generated.jp2");
        
        // Asset
        flags.IsTransient.Should().BeTrue();
        flags.AlreadyUploaded.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeTrue();
        flags.ImageServerFilePath.Should().Be($"/path/to/generated.jp2");
    }
    
    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    [InlineData("image/jpeg")]
    public void Ctor_NotOptimised_NoImageChannel(string mediaType)
    {
        // Arrange
        var context = GetContext(true, false, mediaType, false);
        
        // Act
        var flags = new ImageServerClient.ImageProcessorFlags(context, "/path/to/generated.jp2");
        
        // Asset
        flags.IsTransient.Should().BeFalse();
        flags.AlreadyUploaded.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeTrue();
        flags.ImageServerFilePath.Should().Be($"/path/to/generated.jp2");
    }
    
    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    [InlineData("image/jpeg")]
    public void Ctor_Optimised_NoImageChannelWithFileChannel(string mediaType)
    {
        // Arrange
        var context = GetContext(true, true, mediaType, false);
        
        context.Asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
        {
            Channel = AssetDeliveryChannels.File,
            DeliveryChannelPolicyId = 3
        });
        
        // Act
        var flags = new ImageServerClient.ImageProcessorFlags(context, "/path/to/generated.jp2");
        
        // Asset
        flags.IsTransient.Should().BeTrue();
        flags.AlreadyUploaded.Should().BeTrue();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeTrue();
        flags.ImageServerFilePath.Should().Be($"/path/to/generated.jp2");
    }

    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    [InlineData("image/jpeg")]
    public void Ctor_NotOptimised_NoImageChannelWithFileChannel(string mediaType)
    {
        // Arrange
        var context = GetContext(true, false, mediaType, false);
        
        context.Asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
        {
            Channel = AssetDeliveryChannels.File,
            DeliveryChannelPolicyId = 3
        });
        
        // Act
        var flags = new ImageServerClient.ImageProcessorFlags(context, "/path/to/generated.jp2");
        
        // Asset
        flags.IsTransient.Should().BeFalse();
        flags.AlreadyUploaded.Should().BeTrue();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeTrue();
        flags.ImageServerFilePath.Should().Be($"/path/to/generated.jp2");
    }
    
    private IngestionContext GetContext(bool useOriginal, 
        bool isOptimised, 
        string mediaType = "image/jpeg", 
        bool addImageDeliveryChannel = true)
    {
        var asset = new Asset(new AssetId(1, 2, "foo"))
        {
            DeliveryChannels = new[]
            {
                "iiif-img"

            },
            ImageDeliveryChannels = new List<ImageDeliveryChannel>()
            {
                new()
                {
                    Channel = AssetDeliveryChannels.Thumbnails,
                    DeliveryChannelPolicy = new DeliveryChannelPolicy()
                    {
                        Name = "default",
                        PolicyData = "[\"100\",\"100\"]"
                    },
                    DeliveryChannelPolicyId = 2
                }
            }
        };

        if (addImageDeliveryChannel)
        {
            asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicy = new DeliveryChannelPolicy()
                {
                    Name = useOriginal ? "use-original" : "default"
                },
                DeliveryChannelPolicyId = 1
            });
        }

        var assetFromOrigin = new AssetFromOrigin(asset.Id, 123, "wherever", mediaType)
        {
            CustomerOriginStrategy = new CustomerOriginStrategy { Optimised = isOptimised },
            Location = "/path/to/original"
        };

        return new IngestionContext(asset).WithAssetFromOrigin(assetFromOrigin);
    }
}