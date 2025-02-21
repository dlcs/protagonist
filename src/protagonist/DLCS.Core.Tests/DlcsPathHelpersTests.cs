using System;

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

    // Specific example here is for ARK id https://en.wikipedia.org/wiki/Archival_Resource_Key#Structure
    [Theory]
    [InlineData("https://dlcs.digirati.io/{prefix}/{version}/{customer}/{space}/path/ark:{assetPath}",
        "https://dlcs.digirati.io/images/first-space/path/ark:NAAN___Name")]
    [InlineData("https://dlcs.digirati.io/{prefix}/{version}/{customer}/{space}/path/ark:{assetPath:3US}",
        "https://dlcs.digirati.io/images/first-space/path/ark:NAAN/Name")]
    [InlineData("https://dlcs.digirati.io/{prefix}/{assetPath}/path/ark:{assetPath:3US}",
        "https://dlcs.digirati.io/images/NAAN___Name/path/ark:NAAN/Name")]
    public void GeneratePathFromTemplate_AssetPath_ObeysFormattingInstruction(string template, string expected)
    {
        // Act
        var replaced = DlcsPathHelpers.GeneratePathFromTemplate(template,
            prefix: "images",
            space: "first-space",
            assetPath: "NAAN___Name");

        // Assert
        replaced.Should().Be(expected);
    }
    
    [Fact]
    public void GeneratePathFromTemplate_AssetPath_Throws_IfUnknownFormattingInstruction()
    {
        // Arrange
        const string template = "https://dlcs.digirati.io/{prefix}/{version}/{customer}/{space}/path/ark:{assetPath:XY}";
        
        // Act
        Action action = () => DlcsPathHelpers.GeneratePathFromTemplate(template,
            prefix: "images",
            space: "first-space",
            assetPath: "NAAN___Name");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("'XY' is not a known assetPath format (Parameter 'format')");
    }
}