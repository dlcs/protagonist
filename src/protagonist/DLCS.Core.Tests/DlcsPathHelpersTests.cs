namespace DLCS.Core.Tests;

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
        var replaced = DlcsPathHelpers.GeneratePathFromTemplate(template,
            prefix: "images",
            customer: "18",
            space: "first-space",
            assetPath: "200.jpg");
        
        // Assert
        replaced.Should().Be(expected);
    }
    
    [Fact]
    public void GeneratePathFromTemplate_ReplacesExpectedElements_Versioned()
    {
        // Arrange
        const string template = "dlcs.digirati.io/{prefix}/{version}/{customer}/{space}/path/{assetPath}";
        const string expected = "dlcs.digirati.io/images/v99/18/first-space/path/200.jpg";
        
        // Act
        var replaced = DlcsPathHelpers.GeneratePathFromTemplate(template,
            prefix: "images",
            customer: "18",
            space: "first-space",
            version: "v99",
            assetPath: "200.jpg");
        
        // Assert
        replaced.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://dlcs.digirati.io/{prefix}/{version}/{customer}/{space}/path/{assetPath}",
        "https://dlcs.digirati.io/images/first-space/path/200.jpg")]
    [InlineData("http://dlcs.digirati.io/{prefix}/{version}/{customer}/{space}/path/{assetPath}",
        "http://dlcs.digirati.io/images/first-space/path/200.jpg")]
    [InlineData("http://dlcs.digirati.io//{prefix}/{version}/{customer}/{space}/path/{assetPath}",
        "http://dlcs.digirati.io/images/first-space/path/200.jpg")]
    public void GeneratePathFromTemplate_RemovesDoubleSlashes(string template, string expected)
    {
        // Act
        var replaced = DlcsPathHelpers.GeneratePathFromTemplate(template,
            prefix: "images",
            space: "first-space",
            assetPath: "200.jpg");

        // Assert
        replaced.Should().Be(expected);
    }
}