using FluentAssertions;
using IIIF.ImageApi;
using Xunit;

namespace IIIF.Tests.ImageApi
{
    public class ImageRequestTests
    {
        [Theory]
        [InlineData("/image", "")]
        [InlineData("image", "")]
        [InlineData("/path-prefix/image/", "path-prefix/")]
        [InlineData("path-prefix/image/", "path-prefix/")]
        public void Parse_Correct_BasePath(string path, string prefix)
        {
            // Act
            var result = ImageRequest.Parse(path, prefix);
            
            // Assert
            result.IsBase.Should().BeTrue();
        }
        
        [Theory]
        [InlineData("/image/info.json", "")]
        [InlineData("image/info.json", "")]
        [InlineData("/path-prefix/image/info.json", "path-prefix/")]
        [InlineData("path-prefix/image/info.json", "path-prefix/")]
        public void Parse_Correct_InfoJson(string path, string prefix)
        {
            // Act
            var result = ImageRequest.Parse(path, prefix);
            
            // Assert
            result.IsInformationRequest.Should().BeTrue();
        }
    }
}