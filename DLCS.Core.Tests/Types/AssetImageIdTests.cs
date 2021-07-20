using System;
using DLCS.Core.Types;
using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests.Types
{
    public class AssetImageIdTests
    {
        [Fact]
        public void ToString_CorrectFormat()
        {
            var assetImageId = new AssetImageId(19, 4, "my-first-image");
            const string expected = "19/4/my-first-image";

            assetImageId.ToString().Should().Be(expected);
        }
        
        [Fact]
        public void FromString_ReturnsExpected()
        {
            const string assetId = "19/4/my-first-image";

            var assetImageId = AssetImageId.FromString(assetId);

            assetImageId.Customer.Should().Be(19);
            assetImageId.Space.Should().Be(4);
            assetImageId.Image.Should().Be("my-first-image");
        }

        [Theory]
        [InlineData("image")]
        [InlineData("1/2/image/easrwt")]
        [InlineData("customer/2/image")]
        [InlineData("1/space/image")]
        public void FromString_Throws_IfInvalidFormat(string assetId)
        {
            Action action = () => AssetImageId.FromString(assetId);
            action.Should().Throw<FormatException>();
        }
    }
}