using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests;

public class MIMEHelperTests
{
    [Theory]
    [InlineData("application/pdf", "pdf")]
    [InlineData("image/svg+xml", "svg")]
    [InlineData("image/jpg", "jpg")]
    [InlineData("IMAGE/JPG", "jpg")]
    [InlineData("image/jpg;foo=bar", "jpg")]
    public void GetExtensionForContentType_CorrectForKnownTypes(string contentType, string expected) 
        => MIMEHelper.GetExtensionForContentType(contentType).Should().Be(expected);

    [Fact]
    public void GetExtensionForContentType_ReturnsNullForUnknownTypes()
        => MIMEHelper.GetExtensionForContentType("foo/bar").Should().BeNull();
        
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetExtensionForContentType_ReturnsNullForNullOrWhitespace(string contentType)
        => MIMEHelper.GetExtensionForContentType(contentType).Should().BeNull();
        
    [Theory]
    [InlineData("mp3", "audio/mp3")]
    [InlineData(".mp3", "audio/mp3")]
    [InlineData("SVG", "image/svg+xml")]
    [InlineData("jpg", "image/jpeg")]
    [InlineData("jp2", "image/jp2")]
    public void GetContentTypeForExtension_CorrectForKnownExtension(string extension, string expected) 
        => MIMEHelper.GetContentTypeForExtension(extension).Should().Be(expected);

    [Fact]
    public void GetContentTypeForExtension_ReturnsNullForUnknownExtension()
        => MIMEHelper.GetContentTypeForExtension("foo/bar").Should().BeNull();
        
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetContentTypeForExtension_ReturnsNullForNullOrWhitespace(string contentType)
        => MIMEHelper.GetContentTypeForExtension(contentType).Should().BeNull();
}