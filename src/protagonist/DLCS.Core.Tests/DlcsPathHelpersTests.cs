using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests
{
    public class DlcsPathHelpersTests
    {
        [Fact]
        public void GeneratePathFromTemplate_ReturnsTemplate_IfNoReplacementsFound()
        {
            // Arrange
            const string template = "dlcs.digirati.io/{0}/{1}/{x}";
            
            // Act
            var replaced = DlcsPathHelpers.GeneratePathFromTemplate(template);
            
            // Assert
            replaced.Should().Be(template);
        }
        
        [Fact]
        public void GeneratePathFromTemplate_ReplacesExpectedElements()
        {
            // Arrange
            const string template = "dlcs.digirati.io/{prefix}/{customer}/{space}/path/{assetPath}";
            const string expected = "dlcs.digirati.io/images/18/first-space/path/200.jpg";
            
            // Act
            var replaced = DlcsPathHelpers.GeneratePathFromTemplate(template, "images", "18", "first-space", "200.jpg");
            
            // Assert
            replaced.Should().Be(expected);
        }
    }
}