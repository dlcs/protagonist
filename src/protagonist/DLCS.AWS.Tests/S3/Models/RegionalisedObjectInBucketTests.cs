using DLCS.AWS.S3.Models;
using FluentAssertions;
using Xunit;

namespace DLCS.AWS.Tests.S3.Models
{
    public class RegionalisedObjectInBucketTests
    {
        [Theory]
        [InlineData("s3://eu-west-1/dlcs-storage/2/1/foo-bar", true)]  // this is a non-existent format but it's used by Deliverator 
        [InlineData("s3://dlcs-storage/2/1/foo-bar", false)]  // this is the correct format
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
    }
}