using Amazon.ElasticTranscoder.Model;
using Engine.Ingest.Handlers;
using Engine.Ingest.Timebased;

namespace Engine.Tests.Ingest.Timebased;

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
        var elasticTranscodeMessage = new ElasticTranscoderMessage
        {
            Input = new JobInput(),
            State = state
        };

        var transcodeResult = new TranscodeResult(elasticTranscodeMessage);

        // Assert
        transcodeResult.IsComplete().Should().Be(expected);
    }
}