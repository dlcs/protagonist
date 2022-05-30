using DLCS.AWS.S3;
using DLCS.AWS.Settings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DLCS.AWS.Tests.S3
{
    public class S3StorageKeyGeneratorTests
    {
        private readonly S3StorageKeyGenerator sut;

        public S3StorageKeyGeneratorTests()
        {
            sut = new S3StorageKeyGenerator(Options.Create<AWSSettings>(new AWSSettings
            {
                Region = "eu-west-1",
                S3 = new S3Settings
                {
                    OutputBucket = "test-output",
                    ThumbsBucket = "test-thumbs",
                    StorageBucket = "test-storage"
                }
            }));
        }
        
        [Fact]
        public void GetStorage_ReturnsExpected()
        {
            // Arrange
            const string expected = "10/20/foo-bar";

            // Act
            var actual = sut.GetStorageKey(10, 20, "foo-bar");
            
            // Assert
            actual.Should().Be(expected);
        }
        
        [Fact]
        public void GetStorage_AssetId_ReturnsExpected()
        {
            // Arrange
            const string expected = "10/20/foo-bar";
            var asset = new AssetId(10, 20, "foo-bar");

            // Act
            var actual = sut.GetStorageKey(asset);
            
            // Assert
            actual.Should().Be(expected);
        }

        [Fact]
        public void GetThumbnailLocation_ReturnsExpected_Open()
        {
            // Arrange
            const string expected = "10/20/foo-bar/open/400.jpg";
            var asset = new AssetId(10, 20, "foo-bar");
            
            // Act
            var actual = sut.GetThumbnailLocation(asset, 400);
            
            // Assert
            actual.Key.Should().Be(expected);
            actual.Bucket.Should().Be("test-thumbs");
        }
        
        [Fact]
        public void GetThumbnailLocation_ReturnsExpected_Auth()
        {
            // Arrange
            const string expected = "10/20/foo-bar/auth/200.jpg";
            var asset = new AssetId(10, 20, "foo-bar");
            
            // Act
            var actual = sut.GetThumbnailLocation(asset, 200, false);
            
            // Assert
            actual.Key.Should().Be(expected);
            actual.Bucket.Should().Be("test-thumbs");
        }
        
        [Fact]
        public void GetLegacyThumbnailLocation_ReturnsExpected()
        {
            // Arrange
            const string expected = "10/20/foo-bar/full/200,800/0/default.jpg";
            var asset = new AssetId(10, 20, "foo-bar");
            
            // Act
            var actual = sut.GetLegacyThumbnailLocation(asset, 200, 800);
            
            // Assert
            actual.Key.Should().Be(expected);
            actual.Bucket.Should().Be("test-thumbs");
        }
        
        [Fact]
        public void GetThumbsSizesJsonLocation_ReturnsExpected()
        {
            // Arrange
            const string expected = "10/20/foo-bar/s.json";
            var asset = new AssetId(10, 20, "foo-bar");
            
            // Act
            var actual = sut.GetThumbsSizesJsonLocation(asset);
            
            // Assert
            actual.Key.Should().Be(expected);
            actual.Bucket.Should().Be("test-thumbs");
        }
        
        [Fact]
        public void GetLargestThumbnailLocation_ReturnsExpected()
        {
            // Arrange
            const string expected = "10/20/foo-bar/low.jpg";
            var asset = new AssetId(10, 20, "foo-bar");
            
            // Act
            var actual = sut.GetLargestThumbnailLocation(asset);
            
            // Assert
            actual.Key.Should().Be(expected);
            actual.Bucket.Should().Be("test-thumbs");
        }
        
        [Fact]
        public void GetThumbnailsRoot_ReturnsExpected()
        {
            // Arrange
            const string expected = "10/20/foo-bar/";
            var asset = new AssetId(10, 20, "foo-bar");
            
            // Act
            var actual = sut.GetThumbnailsRoot(asset);
            
            // Assert
            actual.Key.Should().Be(expected);
            actual.Bucket.Should().Be("test-thumbs");
        }
        
        [Fact]
        public void GetOutputLocation_ReturnsExpected()
        {
            // Act
            var actual = sut.GetOutputLocation("foo/bar/baz");
            
            // Assert
            actual.Key.Should().Be("foo/bar/baz");
            actual.Bucket.Should().Be("test-output");
        }

        [Fact]
        public void GetTimebasedAssetLocation_ReturnsExpected()
        {
            // Arrange
            const string expected = "10/20/foo-bar/foo/bar/baz";
            var asset = new AssetId(10, 20, "foo-bar");
            
            // Act
            var actual = sut.GetTimebasedAssetLocation(asset, "foo/bar/baz");
            
            // Assert
            actual.Key.Should().Be(expected);
            actual.Bucket.Should().Be("test-storage");
            actual.Region.Should().Be("eu-west-1");
        }
    }
}