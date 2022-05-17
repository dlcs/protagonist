using DLCS.AWS.S3;
using DLCS.Model.Assets;
using FluentAssertions;
using Xunit;

namespace DLCS.AWS.Tests.S3
{
    public class StorageKeyGeneratorTests
    {
        [Fact]
        public void GetStorage_ReturnsExpected()
        {
            // Arrange
            const string expected = "10/20/foo-bar";

            // Act
            var actual = StorageKeyGenerator.GetStorageKey(10, 20, "foo-bar");
            
            // Assert
            actual.Should().Be(expected);
        }
        
        [Theory]
        [InlineData("foo-bar")]
        [InlineData("/foo-bar")]
        [InlineData("10/20/foo-bar")]
        public void GetStorage_Asset_ReturnsExpected(string id)
        {
            // Arrange
            const string expected = "10/20/foo-bar";
            var asset = new Asset {Id = id, Customer = 10, Space = 20};

            // Act
            var actual = asset.GetStorageKey();
            
            // Assert
            actual.Should().Be(expected);
        }

        [Theory]
        [InlineData("cust/space/image")]
        [InlineData("cust/space/image/")]
        public void GetLargestThumbPath_ReturnsExpected(string assetKey)
        {
            // Arrange
            const string expected = "cust/space/image/low.jpg";
            
            // Act
            var actual = StorageKeyGenerator.GetLargestThumbPath(assetKey);
            
            // Assert
            actual.Should().Be(expected);
        }
    }
}