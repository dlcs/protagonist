using System;
using DLCS.Core.Types;
using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests.Types;

public class AssetImageIdTests
{
    [Fact]
    public void ToString_CorrectFormat()
    {
        var assetImageId = new AssetId(19, 4, "my-first-image");
        const string expected = "19/4/my-first-image";

        assetImageId.ToString().Should().Be(expected);
    }
    
    [Fact]
    public void FromString_ReturnsExpected()
    {
        const string assetId = "19/4/my-first-image";

        var assetImageId = AssetId.FromString(assetId);

        assetImageId.Customer.Should().Be(19);
        assetImageId.Space.Should().Be(4);
        assetImageId.Asset.Should().Be("my-first-image");
    }

    [Theory]
    [InlineData("image")]
    [InlineData("1/2/image/easrwt")]
    public void FromString_Throws_IfInvalidFormat(string assetId)
    {
        Action action = () => AssetId.FromString(assetId);
        action.Should().Throw<FormatException>();
    }
}