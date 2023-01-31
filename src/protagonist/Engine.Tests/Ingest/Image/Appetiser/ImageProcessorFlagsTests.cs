using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Policies;
using Engine.Ingest;
using Engine.Ingest.Image.Appetiser;
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
        Action action = () => new AppetiserClient.ImageProcessorFlags(context);
        
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
        var flags = new AppetiserClient.ImageProcessorFlags(context);
        
        // Asset
        flags.GenerateDerivativesOnly.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeTrue();
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
        var flags = new AppetiserClient.ImageProcessorFlags(context);
        
        // Asset
        flags.GenerateDerivativesOnly.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeFalse();
        flags.SaveInDlcsStorage.Should().BeTrue();
    }

    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    public void Ctor_UseOriginalJP2_NotOptimised(string mediaType)
    {
        // Arrange
        var context = GetContext(true, false, mediaType);
        
        // Act
        var flags = new AppetiserClient.ImageProcessorFlags(context);
        
        // Asset
        flags.GenerateDerivativesOnly.Should().BeTrue();
        flags.OriginIsImageServerReady.Should().BeTrue();
        flags.SaveInDlcsStorage.Should().BeTrue();
    }
    
    [Theory]
    [InlineData("image/jpeg")]
    public void Ctor_UseOriginalNotJP2_NotOptimised(string mediaType)
    {
        // Arrange
        var context = GetContext(true, false, mediaType);
        
        // Act
        var flags = new AppetiserClient.ImageProcessorFlags(context);
        
        // Asset
        flags.GenerateDerivativesOnly.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeTrue();
        flags.SaveInDlcsStorage.Should().BeTrue();
    }

    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    public void Ctor_UseOriginalJP2_Optimised(string mediaType)
    {
        // Arrange
        var context = GetContext(true, true, mediaType);
        
        // Act
        var flags = new AppetiserClient.ImageProcessorFlags(context);
        
        // Asset
        flags.GenerateDerivativesOnly.Should().BeTrue();
        flags.OriginIsImageServerReady.Should().BeTrue();
        flags.SaveInDlcsStorage.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("image/jpeg")]
    public void Ctor_UseOriginalNotJP2_Optimised(string mediaType)
    {
        // Arrange
        var context = GetContext(true, true, mediaType);
        
        // Act
        var flags = new AppetiserClient.ImageProcessorFlags(context);
        
        // Asset
        flags.GenerateDerivativesOnly.Should().BeFalse();
        flags.OriginIsImageServerReady.Should().BeTrue();
        flags.SaveInDlcsStorage.Should().BeFalse();
    }
    
    private IngestionContext GetContext(bool useOriginal, bool isOptimised, string mediaType = "image/jpeg")
    {
        var asset = new Asset(new AssetId(1, 2, "foo"))
            .WithImageOptimisationPolicy(new ImageOptimisationPolicy
                { Id = useOriginal ? "use-original" : "fast-higher" });

        var assetFromOrigin = new AssetFromOrigin(asset.Id, 123, "wherever", mediaType)
        {
            CustomerOriginStrategy = new CustomerOriginStrategy { Optimised = isOptimised }
        };
        
        return new IngestionContext(asset).WithAssetFromOrigin(assetFromOrigin);
    }
}