using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.Model.Tests.Assets;

public class AdjunctXTests
{
    private static Adjunct BuildAdjunct(string origin, bool optimised = false) => new()
    {
        Id = "test-adjunct",
        AssetId = new AssetId(1, 1, "test-asset"),
        MediaType = "image/jpeg",
        IIIFLink = IIIFLinkType.SeeAlso,
        Type = "Image",
        Origin = origin,
        Optimised = optimised,
    };

    [Fact]
    public void IsHosted_ReturnsTrue_WhenOriginSet()
    {
        BuildAdjunct("https://example.com/file.jpg").IsHosted().Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IsHosted_ReturnsFalse_WhenOriginNullOrEmpty(string origin)
    {
        BuildAdjunct(origin).IsHosted().Should().BeFalse();
    }
    
    [Fact]
    public void CountsTowardStoredSize_ReturnsTrue_WhenOriginSet_AndNotOptimised()
    {
        BuildAdjunct("https://example.com/file.jpg").CountsTowardStoredSize().Should().BeTrue();
    }
    
    [Fact]
    public void CountsTowardStoredSize_ReturnsFalse_WhenOriginSet_AndOptimised()
    {
        BuildAdjunct("https://example.com/file.jpg", true).CountsTowardStoredSize().Should().BeFalse();
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CountsTowardStoredSize_ReturnsFalse_WhenOriginNullOrEmpty_AndOptimised(string origin)
    {
        // This isn't a possible real-world scenario
        BuildAdjunct(origin, true).CountsTowardStoredSize().Should().BeFalse();
    }
}
