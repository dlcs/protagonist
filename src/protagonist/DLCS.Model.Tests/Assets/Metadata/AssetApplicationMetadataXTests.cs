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
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetThumbsMetadata_ThrowIfNotFoundTrue_ReturnsThumbs(bool throwIfNotFound)
    {
        var expected = new ThumbnailSizes(
            new List<int[]> { new[] { 606, 1000 }, new[] { 302, 500 } },
            new List<int[]>());
        var thumbsMetadata = new AssetApplicationMetadata
        {
            MetadataType = "ThumbSizes", MetadataValue = "{\"o\":[[606,1000],[302,500]],\"a\":[]}",
        };
        var metadata = new List<AssetApplicationMetadata> { new() { MetadataType = "NotThumbs" }, thumbsMetadata };
        metadata.GetThumbsMetadata(throwIfNotFound).Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void GetTranscodeMetadata_ThrowIfNotFoundFalse_ReturnsNull_IfListNull()
    {
        List<AssetApplicationMetadata> metadata = null;
        metadata.GetTranscodeMetadata(false).Should().BeNull();
    }
    
    [Fact]
    public void GetTranscodeMetadata_ThrowIfNotFoundFalse_ReturnsNull_IfListEmpty()
    {
        var metadata = new List<AssetApplicationMetadata>();
        metadata.GetTranscodeMetadata(false).Should().BeNull();
    }
    
    [Fact]
    public void GetTranscodeMetadata_ThrowIfNotFoundFalse_ReturnsNull_IfTranscodesNotFound()
    {
        var metadata = new List<AssetApplicationMetadata> { new() { MetadataType = "NotAV" } };
        metadata.GetTranscodeMetadata(false).Should().BeNull();
    }
    
    [Fact]
    public void GetTranscodeMetadata_ThrowIfNotFoundTrue_Throws_IfListNull()
    {
        List<AssetApplicationMetadata> metadata = null;
        Action action = () => metadata.GetTranscodeMetadata(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
    
    [Fact]
    public void GetTranscodeMetadata_ThrowIfNotFoundTrue_Throws_IfListEmpty()
    {
        var metadata = new List<AssetApplicationMetadata>();
        Action action = () => metadata.GetTranscodeMetadata(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
    
    [Fact]
    public void GetTranscodeMetadata_ThrowIfNotFoundTrue_Throws_IfThumbsNotFound()
    {
        var metadata = new List<AssetApplicationMetadata> { new() { MetadataType = "NotThumbs" } };
        Action action = () => metadata.GetTranscodeMetadata(true);

        action.Should().ThrowExactly<InvalidOperationException>();
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetTranscodeMetadata_ReturnsTranscode_IfFound(bool throwIfNotFound)
    {
        var expected = new AVTranscode[]
        {
            new()
            {
                Duration = 100, Extension = "mp3", Location = new Uri("s3://bucket/location.mp3"),
                TranscodeName = "the MP3 one", MediaType = "audio/mp3"
            },
            new()
            {
                Duration = 120, Extension = "mp4", Location = new Uri("s3://bucket/path/location.mp4"),
                TranscodeName = "the MP4 one", MediaType = "video/mp4", Width = 10, Height = 20
            }
        };
        var avMetadata = new AssetApplicationMetadata
        {
            MetadataType = "AVTranscodes",
            MetadataValue =
                "[ {\"l\":\"s3://bucket/location.mp3\",\"n\":\"the MP3 one\",\"ex\":\"mp3\",\"mt\":\"audio/mp3\",\"d\":100}, {\"l\":\"s3://bucket/path/location.mp4\",\"n\":\"the MP4 one\",\"ex\":\"mp4\",\"mt\":\"video/mp4\",\"w\":10,\"h\":20,\"d\":120} ]",
        };
        var metadata = new List<AssetApplicationMetadata> { new() { MetadataType = "NotThumbs" }, avMetadata };
        metadata.GetTranscodeMetadata(throwIfNotFound).Should().BeEquivalentTo(expected);
    }
}
