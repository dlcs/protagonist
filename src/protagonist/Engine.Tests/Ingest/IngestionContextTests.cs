using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Ingest;
using Engine.Ingest.Persistence;

namespace Engine.Tests.Ingest;

public class IngestionContextTests
{
    [Theory]
    [InlineData(100L, 200L)]
    [InlineData(100L, null)]
    [InlineData(null, 200L)]
    [InlineData(null, null)]
    public void WithStorage_SetsValue_IfDoesNotExist(long? size, long? thumb)
    {
        // Arrange
        var assetId = new AssetId(10, 20, "foo");
        var sut = new IngestionContext(new Asset(assetId));
        
        // Act
        sut.WithStorage(size, thumb);
        
        // Assert
        sut.ImageStorage.Id.Should().Be(assetId);
        sut.ImageStorage.Customer.Should().Be(assetId.Customer);
        sut.ImageStorage.Space.Should().Be(assetId.Space);
        sut.ImageStorage.Size.Should().Be(size ?? 0);
        sut.ImageStorage.ThumbnailSize.Should().Be(thumb ?? 0);
        sut.ImageStorage.LastChecked.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
    
    [Theory]
    [InlineData(100L, 200L)]
    [InlineData(100L, null)]
    [InlineData(null, 200L)]
    [InlineData(null, null)]
    public void WithStorage_IncrementsImageStorageValue_IfExists(long? size, long? thumb)
    {
        // Arrange
        var assetId = new AssetId(10, 20, "foo");
        var sut = new IngestionContext(new Asset(assetId));
        sut.WithStorage(50, 10);
        
        // Act
        sut.WithStorage(size, thumb);
        
        // Assert
        sut.ImageStorage.Id.Should().Be(assetId);
        sut.ImageStorage.Customer.Should().Be(assetId.Customer);
        sut.ImageStorage.Space.Should().Be(assetId.Space);
        sut.ImageStorage.Size.Should().Be((size ?? 0) + 50);
        sut.ImageStorage.ThumbnailSize.Should().Be((thumb ?? 0) + 10);
        sut.ImageStorage.LastChecked.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("image/jpeg")]
    [InlineData("image/unknown")]
    public void UpdateMediaTypeIfRequired_NoOp_IfAssetFromOriginNull(string? mediaType)
    {
        var asset = new Asset(new AssetId(10, 20, "foo")) { MediaType = mediaType };
        new IngestionContext(asset).UpdateMediaTypeIfRequired();

        asset.MediaType.Should().Be(mediaType, "MediaType untouched as no AssetFromOrigin");
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UpdateMediaTypeIfRequired_NoOp_IfAssetFromOriginContentTypeNullOrEmpty(string? contentType)
    {
        var asset = new Asset(new AssetId(10, 20, "foo")) { MediaType = "image/unknown" };
        new IngestionContext(asset)
            .WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 10, "hi", contentType))
            .UpdateMediaTypeIfRequired();

        asset.MediaType.Should().Be("image/unknown", "MediaType untouched as AssetFromOrigin unknown contentType");
    }
    
    [Fact]
    public void UpdateMediaTypeIfRequired_NoOp_IfMediaType_NotImageUnknown()
    {
        var asset = new Asset(new AssetId(10, 20, "foo")) { MediaType = "image/wellknown" };
        new IngestionContext(asset)
            .WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 10, "hi", "image/jpeg"))
            .UpdateMediaTypeIfRequired();

        asset.MediaType.Should().Be("image/wellknown", "MediaType untouched as mediaType known");
    }
    
    [Fact]
    public void UpdateMediaTypeIfRequired_UpdatesMediaType_IfImageUnknown()
    {
        var asset = new Asset(new AssetId(10, 20, "foo")) { MediaType = "image/unknown" };
        new IngestionContext(asset)
            .WithAssetFromOrigin(new AssetFromOrigin(asset.Id, 10, "hi", "image/jpeg"))
            .UpdateMediaTypeIfRequired();

        asset.MediaType.Should().Be("image/jpeg", "MediaType updates to match AssetFromOrigin");
    }
}
