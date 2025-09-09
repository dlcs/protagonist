using DLCS.AWS.Transcoding;
using DLCS.Core.Types;

namespace DLCS.AWS.Tests.Transcoding;

public class TranscoderTemplatesTests
{
    [Fact]
    public void GetTranscodeKey_Null_IfPresetNotInExpectedFormat()
    {
        // Act
        var template = TranscoderTemplates.GetTranscodeKey("video/mpg", new AssetId(1, 2, "foo"), null);

        // Assert
        template.Should().BeNull();
    }

    [Fact]
    public void GetTranscodeKey_ReturnsExpected_IfAudio()
    {
        // Arrange
        var asset = new AssetId(1, 5, "foo");
        const string expected = "1/5/foo/full/max/default.mp3";

        // Act
        var template = TranscoderTemplates.GetTranscodeKey("audio/wav", asset, "mp3");

        // Assert
        template.Should().Be(expected);
    }

    [Fact]
    public void GetTranscodeKey_ReturnsExpected_IfVideo()
    {
        // Arrange
        var asset = new AssetId(1, 5, "foo");
        const string expected = "1/5/foo/full/full/max/max/0/default.webm";

        // Act
        var template = TranscoderTemplates.GetTranscodeKey("video/mpeg", asset, "webm");

        // Assert
        template.Should().Be(expected);
    }

    [Fact]
    public void GetTranscodeKey_Throws_IfNonAudioOrVideoContentType()
    {
        // Act
        Action action = () =>
            TranscoderTemplates.GetTranscodeKey("binary/octet-stream", new AssetId(1, 5, "foo"), "webm");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to determine target location for mediaType 'binary/octet-stream'");
    }

    [Fact]
    public void GetDestinationTemplate_ReturnsExpected_IfAudio()
    {
        // Arrange
        const string expected = "{asset}/full/max/default.{extension}";

        // Act
        var template = TranscoderTemplates.GetDestinationTemplate("audio/wav");

        // Assert
        template.Should().Be(expected);
    }

    [Fact]
    public void GetDestinationTemplate_ReturnsExpected_IfVideo()
    {
        // Arrange
        const string expected = "{asset}/full/full/max/max/0/default.{extension}";

        // Act
        var template = TranscoderTemplates.GetDestinationTemplate("video/mpeg");

        // Assert
        template.Should().Be(expected);
    }

    [Fact]
    public void GetDestinationTemplate_Throws_IfNonAudioOrVideoContentType()
    {
        // Act
        Action action = () =>
            TranscoderTemplates.GetDestinationTemplate("binary/octet-stream");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to determine target location for mediaType 'binary/octet-stream'");
    }
}
