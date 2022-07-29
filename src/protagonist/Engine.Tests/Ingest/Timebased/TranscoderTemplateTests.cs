using DLCS.Core.Types;
using Engine.Ingest.Timebased;

namespace Engine.Tests.Ingest.Timebased;

public class TranscoderTemplatesTests
{
    [Fact]
    public void GetDestinationPath_Null_IfPresetNoInExpectedFormat()
    {
        // Act
        var (template, preset) =
            TranscoderTemplates.ProcessPreset("video/mpg", new AssetId(1, 2, "foo"), "mp3preset", "foo");
            
        // Assert
        template.Should().BeNull();
        preset.Should().BeNull();
    }
        
    [Fact]
    public void GetDestinationPath_ReturnsExpected_IfAudio()
    {
        // Arrange
        var asset = new AssetId(1, 5, "foo");
        const string expected = "_jobid_/1/5/foo/full/max/default.mp3";
            
        // Act
        var (template, preset) =
            TranscoderTemplates.ProcessPreset("audio/wav", asset, "my-preset(mp3)", "_jobid_");
            
        // Assert
        template.Should().Be(expected);
        preset.Should().Be("my-preset");
    }
        
    [Fact]
    public void GetDestinationPath_ReturnsExpected_IfVideo()
    {
        // Arrange
        var asset = new AssetId(1, 5, "foo");
        const string expected = "_jobid_/1/5/foo/full/full/max/max/0/default.webm";
            
        // Act
        var (template, preset) =
            TranscoderTemplates.ProcessPreset("video/mpeg", asset, "my-preset(webm)", "_jobid_");
            
        // Assert
        template.Should().Be(expected);
        preset.Should().Be("my-preset");
    }
        
    [Fact]
    public void GetDestinationPath_Throws_IfNonAudioOrVideoContentType()
    {
        // Act
        Action action = () =>
            TranscoderTemplates.ProcessPreset("binary/octet-stream", new AssetId(1, 5, "foo"), "my-preset(webm)",
                "_jobid_");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to determine target location for mediaType 'binary/octet-stream'");
    }
}