using DLCS.AWS.MediaConvert.Models;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;

namespace DLCS.AWS.Tests.ElasticTranscoder.Model;

public class TranscodeOutputTests
{
    [Theory]
    [InlineData("Progressing", false)]
    [InlineData("Complete", true)]
    [InlineData("COMPLETE", true)]
    [InlineData("Warning", false)]
    [InlineData("Error", false)]
    public void IsComplete_CorrectForStatus(string status, bool expected)
    {
        // Arrange
        var transcodeOutput = new TranscodeOutput { Status = status };

        // Assert
        transcodeOutput.IsComplete().Should().Be(expected);
    }

    [Fact]
    public void GetDuration_ReturnsDurationMillis_IfPresent()
    {
        // Arrange
        var duration = 120L;
        var durationMs = 124567L;
        var transcodeOutput = new TranscodeOutput { Duration = duration, DurationMillis = durationMs };
        
        // Act
        var result = transcodeOutput.GetDuration();
        
        // Assert
        result.Should().Be(durationMs);
    }
    
    [Fact]
    public void GetDuration_ReturnsDurationAsMs_IfDurationMillisNotPresent()
    {
        // Arrange
        var duration = 120L;
        var transcodeOutput = new TranscodeOutput { Duration = duration };
        
        // Act
        var result = transcodeOutput.GetDuration();
        
        // Assert
        result.Should().Be(120000L);
    }
}
