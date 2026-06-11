using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.Model.Tests.Assets;

public class AdjunctXTests
{
    private static Adjunct BuildAdjunct(string origin) => new()
    {
        Id = "test-adjunct",
        AssetId = new AssetId(1, 1, "test-asset"),
        MediaType = "image/jpeg",
        IIIFLink = IIIFLinkType.SeeAlso,
        Type = "Image",
        Origin = origin,
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
}
