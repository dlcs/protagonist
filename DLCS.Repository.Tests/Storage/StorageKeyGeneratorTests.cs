using DLCS.Model.Assets;
using DLCS.Repository.Storage;
using FluentAssertions;
using Xunit;

namespace DLCS.Repository.Tests.Storage
{
    public class StorageKeyGeneratorTests
    {
        [Fact]
        public void GetStorage_ReturnsExpected()
        {
            // Arrange
            const string expected = "10/20/foo-bar";

            // Assert
            var actual = StorageKeyGenerator.GetStorageKey(10, 20, "foo-bar");
            
            // Arrange
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

            // Assert
            var actual = asset.GetStorageKey();
            
            // Arrange
            actual.Should().Be(expected);
        }
    }
}