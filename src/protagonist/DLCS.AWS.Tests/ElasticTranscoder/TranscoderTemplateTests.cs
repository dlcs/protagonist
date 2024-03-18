using DLCS.AWS.ElasticTranscoder;
using DLCS.Core.Types;

namespace DLCS.AWS.Tests.ElasticTranscoder;

public class TranscoderTemplatesTests
{
    [Fact]
    public void GetDestinationPath_Null_IfPresetNoInExpectedFormat()
    {
        // Act
        var (template, preset) =
            TranscoderTemplates.ProcessPreset("video/mpg", new AssetId(1, 2, "foo"), "mp3preset", "foo", null);
            
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
            TranscoderTemplates.ProcessPreset("audio/wav", asset, "some preset", "_jobid_", "mp3");
            
        // Assert
        template.Should().Be(expected);
        preset.Should().Be("some preset");
    }
        
    [Fact]
    public void GetDestinationPath_ReturnsExpected_IfVideo()
    {
        // Arrange
        var asset = new AssetId(1, 5, "foo");
        const string expected = "_jobid_/1/5/foo/full/full/max/max/0/default.webm";
            
        // Act
        var (template, preset) =
            TranscoderTemplates.ProcessPreset("video/mpeg", asset, "some preset", "_jobid_", "webm");
            
        // Assert
        template.Should().Be(expected);
        preset.Should().Be("some preset");
    }
        
    [Fact]
    public void GetDestinationPath_Throws_IfNonAudioOrVideoContentType()
    {
        // Act
        Action action = () =>
            TranscoderTemplates.ProcessPreset("binary/octet-stream", new AssetId(1, 5, "foo"), 
                "some preset",
                "_jobid_",
                "webm");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to determine target location for mediaType 'binary/octet-stream'");
    }

    [Fact]
    public void GetFinalDestinationKey_Correct_IfAudio()
    {
        // Arrange
        var asset = new AssetId(1, 5, "foo");
        const string expected = "1/5/foo/full/max/default.mp3";
        var (template, _) =
            TranscoderTemplates.ProcessPreset("audio/wav", asset, "some preset", Guid.NewGuid().ToString(), "mp3");
            
        // Act
        var result = TranscoderTemplates.GetFinalDestinationKey(template);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetFinalDestinationKey_Correct_IfVideo()
    {
        // Arrange
        var asset = new AssetId(1, 5, "foo");
        const string expected = "1/5/foo/full/full/max/max/0/default.webm";
        var (template, _) =
            TranscoderTemplates.ProcessPreset("video/mpeg", asset, "some preset", Guid.NewGuid().ToString(), "webm");
            
        // Act
        var result = TranscoderTemplates.GetFinalDestinationKey(template);
        
        // Assert
        result.Should().Be(expected);
    }
}