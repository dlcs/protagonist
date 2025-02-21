using System;
using System.Collections.Generic;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;

namespace DLCS.Model.Tests.Assets.Metadata;

public class AssetApplicationMetadataXTests
{
    [Fact]
    public void GetThumbsMetadata_ThrowIfNotFoundFalse_ReturnsNull_IfListNull()
    {
        List<AssetApplicationMetadata> metadata = null;
        metadata.GetThumbsMetadata(false).Should().BeNull();
    }
    
    [Fact]
    public void GetThumbsMetadata_ThrowIfNotFoundFalse_ReturnsNull_IfListEmpty()
    {
        var metadata = new List<AssetApplicationMetadata>();
        metadata.GetThumbsMetadata(false).Should().BeNull();
    }
    
    [Fact]
    public void GetThumbsMetadata_ThrowIfNotFoundFalse_ReturnsNull_IfThumbsNotFound()
    {
        var metadata = new List<AssetApplicationMetadata> { new() { MetadataType = "NotThumbs" } };
        metadata.GetThumbsMetadata(false).Should().BeNull();
    }
    
    [Fact]
    public void GetThumbsMetadata_ThrowIfNotFoundFalse_ReturnsThumbSizes()
    {
        var expected = new ThumbnailSizes(
            new List<int[]> { new[] { 606, 1000 }, new[] { 302, 500 } },
            new List<int[]>());
        var thumbsMetadata = new AssetApplicationMetadata
        {
            MetadataType = "ThumbSizes", MetadataValue = "{\"o\":[[606,1000],[302,500]],\"a\":[]}",
        };
        var metadata = new List<AssetApplicationMetadata> { new() { MetadataType = "NotThumbs" }, thumbsMetadata };
        metadata.GetThumbsMetadata(false).Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void GetThumbsMetadata_ThrowIfNotFoundTrue_Throws_IfListNull()
    {
        List<AssetApplicationMetadata> metadata = null;
        Action action = () => metadata.GetThumbsMetadata(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
    
    [Fact]
    public void GetThumbsMetadata_ThrowIfNotFoundTrue_Throws_IfListEmpty()
    {
        var metadata = new List<AssetApplicationMetadata>();
        Action action = () => metadata.GetThumbsMetadata(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
    
    [Fact]
    public void GetThumbsMetadata_ThrowIfNotFoundTrue_Throws_IfThumbsNotFound()
    {
        var metadata = new List<AssetApplicationMetadata> { new() { MetadataType = "NotThumbs" } };
        Action action = () => metadata.GetThumbsMetadata(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
    
    [Fact]
    public void GetThumbsMetadata_ThrowIfNotFoundTrue_ReturnsThumbs()
    {
        var expected = new ThumbnailSizes(
            new List<int[]> { new[] { 606, 1000 }, new[] { 302, 500 } },
            new List<int[]>());
        var thumbsMetadata = new AssetApplicationMetadata
        {
            MetadataType = "ThumbSizes", MetadataValue = "{\"o\":[[606,1000],[302,500]],\"a\":[]}",
        };
        var metadata = new List<AssetApplicationMetadata> { new() { MetadataType = "NotThumbs" }, thumbsMetadata };
        metadata.GetThumbsMetadata(true).Should().BeEquivalentTo(expected);
    }
}