using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Policies;
using Engine.Ingest;
using Engine.Ingest.Image.ImageServer;
using Engine.Ingest.Image.ImageServer.Clients;
using Engine.Ingest.Persistence;

namespace Engine.Tests.Ingest.Image.ImageServer;

public class ImageProcessorFlagsTests
{
    [Fact]
    public void Ctor_Throws_IfAssetFromOriginNull()
    {
        // Arrange
        var context = new IngestionContext(new Asset());
        
        // Act
        Action action = () => new AppetiserImageProcessor.ImageProcessorFlags(context);
        
        // Asset
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_DoNotUseOriginal_NotOptimised()
    {
        // Arrange
        var context = GetContext(false, false, "image/tiff");
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.HaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeTrue();
        flags.HasImageDeliveryChannel.Should().BeTrue();
    }

    [Fact]
    public void Ctor_DoNotUseOriginal_Optimised()
    {
        // Arrange
        var context = GetContext(false, true, "image/tiff");
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.HaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeTrue();
        flags.HasImageDeliveryChannel.Should().BeTrue();
    }
    
    [Fact]
    public void Ctor_DoNotUseOriginal_Optimised_NoImageChannel()
    {
        // Arrange
        var context = GetContext(false, true, "image/tiff", false);
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.NotHaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeFalse();
        flags.HasImageDeliveryChannel.Should().BeFalse();
    }
    
    [Fact]
    public void Ctor_DoNotUseOriginal_NotOptimised_NoImageChannel()
    {
        // Arrange
        var context = GetContext(false, false, "image/jp2", false);
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.NotHaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeFalse();
        flags.HasImageDeliveryChannel.Should().BeFalse();
    }
    
    [Fact]
    public void Ctor_DoNotUseOriginal_Optimised_NoImageChannelWithFileChannel()
    {
        // Arrange
        var context = GetContext(false, true, "image/tiff", false);
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        context.Asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel
        {
            Channel = AssetDeliveryChannels.File,
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
        });
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.NotHaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeFalse();
        flags.HasImageDeliveryChannel.Should().BeFalse();
    }
    
    [Fact]
    public void Ctor_DoNotUseOriginal_NotOptimised_NoImageChannelWithFileChannel()
    {
        // Arrange
        var context = GetContext(false, false, "image/jp2", false);
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        context.Asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel
        {
            Channel = AssetDeliveryChannels.File,
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
        });
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.NotHaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeFalse();
        flags.HasImageDeliveryChannel.Should().BeFalse();
    }

    [Fact]
    public void Ctor_UseOriginal_NotOptimised()
    {
        // Arrange
        var context = GetContext(true, false, "image/jp2");
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.NotHaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeTrue();
        flags.SaveInDlcsStorage.Should().BeTrue();
        flags.HasImageDeliveryChannel.Should().BeTrue();
    }
   
    [Fact]
    public void Ctor_UseOriginal_Optimised()
    {
        // Arrange
        var context = GetContext(true, true, "image/tiff");
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.NotHaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeTrue();
        flags.SaveInDlcsStorage.Should().BeFalse();
        flags.HasImageDeliveryChannel.Should().BeTrue();
    }
    
    [Fact]
    public void Ctor_UseOriginal_Optimised_NoImageChannel()
    {
        // Arrange
        var context = GetContext(true, true, "image/tiff", false);
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.NotHaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeFalse();
        flags.HasImageDeliveryChannel.Should().BeFalse();
    }
    
    [Fact]
    public void Ctor_UseOriginal_NotOptimised_NoImageChannel()
    {
        // Arrange
        var context = GetContext(true, false, "image/jp2", false);
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.NotHaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeFalse();
        flags.HasImageDeliveryChannel.Should().BeFalse();
    }
    
    [Fact]
    public void Ctor_Optimised_NoImageChannelWithFileChannel()
    {
        // Arrange
        var context = GetContext(true, true, "image/jp2", false);
        
        context.Asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel
        {
            Channel = AssetDeliveryChannels.File,
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
        });
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.NotHaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeTrue();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeFalse();
    }

    [Fact]
    public void Ctor_NotOptimised_NoImageChannelWithFileChannel()
    {
        // Arrange
        var context = GetContext(true, false, "image/jp2", false);
        
        context.Asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel
        {
            Channel = AssetDeliveryChannels.File,
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
        });
        
        // Act
        var flags = new AppetiserImageProcessor.ImageProcessorFlags(context);
        
        // Asset
        flags.Operations.Should()
            .HaveFlag(ImageProcessorOperations.Thumbnails)
            .And.NotHaveFlag(ImageProcessorOperations.Derivative);
        flags.AlreadyUploadedNoImage.Should().BeTrue();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeFalse();
    }
    
    private static IngestionContext GetContext(bool useOriginal, 
        bool isOptimised, 
        string mediaType = "image/jpeg", 
        bool addImageDeliveryChannel = true)
    {
        var asset = new Asset(new AssetId(1, 2, "foo"))
        {
            DeliveryChannels = ["iiif-img"],
            ImageDeliveryChannels = new List<ImageDeliveryChannel>
            {
                new()
                {
                    Channel = AssetDeliveryChannels.Thumbnails,
                    DeliveryChannelPolicy = new DeliveryChannelPolicy
                    {
                        Name = "default",
                        PolicyData = "[\"100,\",\",100\"]"
                    },
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault
                }
            }
        };

        if (addImageDeliveryChannel)
        {
            asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicy = new DeliveryChannelPolicy
                {
                    Name = useOriginal ? "use-original" : "default"
                },
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
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
