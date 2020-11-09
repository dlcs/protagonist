using System;
using DLCS.Model.Storage;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Storage
{
    public class RegionalisedObjectInBucketTests
    {
        [Theory]
        [InlineData("s3://eu-west-1/dlcs-storage/2/1/foo-bar", true)]
        [InlineData("http://s3-eu-west-1.amazonaws.com/dlcs-storage/2/1/foo-bar", true)]
        [InlineData("https://s3.eu-west-1.amazonaws.com/dlcs-storage/2/1/foo-bar", true)]
        [InlineData("http://dlcs-storage.s3.amazonaws.com/2/1/foo-bar", false)]
        [InlineData("https://dlcs-storage.s3.amazonaws.com/2/1/foo-bar", false)]
        [InlineData("http://dlcs-storage.s3.eu-west-1.amazonaws.com/2/1/foo-bar", true)]
        [InlineData("https://dlcs-storage.s3.eu-west-1.amazonaws.com/2/1/foo-bar", true)]
        [InlineData("http://s3.amazonaws.com/dlcs-storage/2/1/foo-bar", false)]
        [InlineData("https://s3.amazonaws.com/dlcs-storage/2/1/foo-bar", false)]
        public void Parse_Correct_Http1(string uri, bool hasRegion)
        {
            // Arrange
            var expected = new RegionalisedObjectInBucket(
                "dlcs-storage",
                "2/1/foo-bar",
                hasRegion ? "eu-west-1" : null);

            // Act
            var actual = RegionalisedObjectInBucket.Parse(uri);

            // Assert
            actual.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void Parse_Null_IfNoMatches()
        {
            // Arrange
            const string uri = "http://example.org";

            // Act
            var actual = RegionalisedObjectInBucket.Parse(uri);

            // Assert
            actual.Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void GetS3QualifiedUri_Throws_IfRegionNullOrEmpty(string region)
        {
            // Arrange
            var bucket = new RegionalisedObjectInBucket("dlcs-storage", "2/1/foo-bar");
            bucket.Region = region;

            Action action = () => bucket.GetS3QualifiedUri();

            // Assert
            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetS3QualifiedUri_Correct()
        {
            // Arrange
            var bucket = new RegionalisedObjectInBucket(
                "dlcs-storage",
                "2/1/foo-bar",
                "eu-west-1");
            const string expected = "s3://eu-west-1/dlcs-storage/2/1/foo-bar";

            // Act
            var actual = bucket.GetS3QualifiedUri();
            
            // Assert
            actual.Should().Be(expected);
        }
    }
}