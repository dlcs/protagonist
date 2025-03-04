namespace DLCS.Core.Tests;

public class MIMEHelperTests
{
    [Theory]
    [InlineData("application/pdf", "pdf")]
    [InlineData("image/svg+xml", "svg")]
    [InlineData("image/jpeg", "jpg")]
    [InlineData("IMAGE/JPEG", "jpg")]
    [InlineData("image/jpeg;foo=bar", "jpg")]
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

    [Theory]
    [InlineData("audio/mp4")]
    [InlineData("audio/mp3")]
    public void IsAudio_True(string mediaType)
        => MIMEHelper.IsAudio(mediaType).Should().BeTrue();
    
    [Theory]
    [InlineData("x-audio/mp4")]
    [InlineData("video/mp4")]
    [InlineData("image/jpeg")]
    [InlineData("")]
    [InlineData(null)]
    public void IsAudio_False(string mediaType)
        => MIMEHelper.IsAudio(mediaType).Should().BeFalse();
    
    [Theory]
    [InlineData("video/mp4")]
    [InlineData("video/mpeg")]
    public void IsVideo_True(string mediaType)
        => MIMEHelper.IsVideo(mediaType).Should().BeTrue();
    
    [Theory]
    [InlineData("x-video/mp4")]
    [InlineData("audio/mp3")]
    [InlineData("image/jpeg")]
    [InlineData("")]
    [InlineData(null)]
    public void IsVideo_False(string mediaType)
        => MIMEHelper.IsVideo(mediaType).Should().BeFalse();
    
    [Theory]
    [InlineData("image/tiff")]
    [InlineData("image/jpeg")]
    public void IsImage_True(string mediaType)
        => MIMEHelper.IsImage(mediaType).Should().BeTrue();
    
    [Theory]
    [InlineData("x-video/mp4")]
    [InlineData("audio/mp3")]
    [InlineData("x-image/jpeg")]
    [InlineData("")]
    [InlineData(null)]
    public void IsImage_False(string mediaType)
        => MIMEHelper.IsImage(mediaType).Should().BeFalse();

    [Theory]
    [InlineData("image/png", "Image")]
    [InlineData("video/mp4", "Video")]
    [InlineData("audio/mp4", "Sound")]
    [InlineData("text/plain", "Text")]
    [InlineData("model/obj", "Model")]
    [InlineData("application/pdf", "DataSet")]
    [InlineData(null, "DataSet")]
    [InlineData("", "DataSet")]
    public void GetRdfType_Correct(string mediaType, string expectedRdfType)
        => MIMEHelper.GetRdfType(mediaType).Should().Be(expectedRdfType);
}
