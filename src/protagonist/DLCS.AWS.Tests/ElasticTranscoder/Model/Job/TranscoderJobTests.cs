using DLCS.AWS.Transcoding.Models.Job;

namespace DLCS.AWS.Tests.ElasticTranscoder.Model.Job;

public class TranscoderJobTests
{
    /*[Theory]
    [InlineData("ac232ab4-c123-4a68-8562-2d9f1a7908fa/2/1/asset-id/full/full/max/max/0/default.mp4", "Protagonist")]
    [InlineData("x/0127/2/1/asset-id/full/full/max/max/0/default.mp4", "Deliverator")]
    public void TranscoderOutput_Create_SetsKeyForIncomplete(string key, string type)
    {
        // Arrange
        var jobOutput = new JobOutput { Key = key, Status = "Progressing" };
        
        // Act
        var parsed = TranscoderJob.TranscoderOutput.Create(jobOutput, "foo");
        
        // Assert
        parsed.TranscodeKey.Should().Be(key, $"Handle {type} format");
    }

    [Fact]
    public void TranscoderOutput_Create_SetsCorrectKey_IfComplete_DeliveratorFormat()
    {
        // Arrange
        const string key = "x/0127/2/1/asset-id/full/full/max/max/0/default.mp4";
        const string expected = "2/1/asset-id/full/full/max/max/0/default.mp4";
        
        var jobOutput = new JobOutput { Key = key, Status = "Complete" };
        
        // Act
        var parsed = TranscoderJob.TranscoderOutput.Create(jobOutput, "ac232ab4-c123-4a68-8562-2d9f1a7908fa");
        
        // Assert
        parsed.TranscodeKey.Should().Be(expected);
    }
    
    [Fact]
    public void TranscoderOutput_Create_SetsCorrectKey_IfComplete_ProtagonistFormat()
    {
        // Arrange
        const string key = "ac232ab4-c123-4a68-8562-2d9f1a7908fa/2/1/asset-id/full/full/max/max/0/default.mp4";
        const string expected = "2/1/asset-id/full/full/max/max/0/default.mp4";
        
        var jobOutput = new JobOutput { Key = key, Status = "Complete" };
        
        // Act
        var parsed = TranscoderJob.TranscoderOutput.Create(jobOutput, "ac232ab4-c123-4a68-8562-2d9f1a7908fa");
        
        // Assert
        parsed.TranscodeKey.Should().Be(expected);
    }
    
    [Fact]
    public void TranscoderOutput_Create_SetsOriginalKey_IfComplete_ProtagonistFormat_JobIdDoesNotMatchPrefix()
    {
        // Arrange
        const string key = "ac232ab4-c123-4a68-8562-2d9f1a7908fa/2/1/asset-id/full/full/max/max/0/default.mp4";
        
        var jobOutput = new JobOutput { Key = key, Status = "Complete" };
        
        // Act
        var parsed = TranscoderJob.TranscoderOutput.Create(jobOutput, "c4b06959-a27e-4c5d-9daa-267648687009");
        
        // Assert
        parsed.TranscodeKey.Should().Be(key);
    }*/
}
