using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;

namespace DLCS.AWS.Tests.ElasticTranscoder.Model;

public class TranscodeResultTests
{
    [Theory]
    [InlineData("PROGRESSING", false)]
    [InlineData("COMPLETED", true)]
    [InlineData("completed", true)]
    [InlineData("WARNING", false)]
    [InlineData("ERROR", false)]
    public void IsComplete_CorrectForState(string state, bool expected)
    {
        // Arrange
        var elasticTranscodeMessage = new TranscodedNotification
        {
            Input = new JobInput(),
            State = state
        };

        var transcodeResult = new TranscodeResult(elasticTranscodeMessage);

        // Assert
        transcodeResult.IsComplete().Should().Be(expected);
    }
    
    [Fact]
    public void GetStoredOriginalAssetSize_Correct_IfMissing()
    {
        // Arrange
        var elasticTranscodeMessage = new TranscodedNotification
        {
            Input = new JobInput(),
            UserMetadata = new Dictionary<string, string>()
        };

        var transcodeResult = new TranscodeResult(elasticTranscodeMessage);
        
        // Assert
        transcodeResult.GetStoredOriginalAssetSize().Should().Be(0);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("foo", 0)]
    [InlineData("0", 0)]
    [InlineData("990", 990)]
    public void GetStoredOriginalAssetSize_Correct(string input, long expected)
    {
        // Arrange
        var elasticTranscodeMessage = new TranscodedNotification
        {
            Input = new JobInput(),
            UserMetadata = new Dictionary<string, string>
            {
                [TranscodeMetadataKeys.OriginSize] = input
            }
        };

        var transcodeResult = new TranscodeResult(elasticTranscodeMessage);
        
        // Assert
        transcodeResult.GetStoredOriginalAssetSize().Should().Be(expected);
    }
}