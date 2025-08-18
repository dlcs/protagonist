using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Ingest;

namespace Engine.Tests.Ingest;

public class IngestionContextTests
{
    [Theory]
    [InlineData(100L, 200L)]
    [InlineData(100L, null)]
    [InlineData(null, 200L)]
    [InlineData(null, null)]
    public void WithStorage_SetsValue_IfDoesnotExist(long? size, long? thumb)
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
}
