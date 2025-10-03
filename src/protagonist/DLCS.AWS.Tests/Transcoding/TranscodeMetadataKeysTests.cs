using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models.Job;
using DLCS.Core.Types;

namespace DLCS.AWS.Tests.Transcoding;

public class TranscodeMetadataKeysTests
{
    [Fact]
    public void GetAssetId_ReturnsNull_IfMissing()
    {
        // Arrange
        var transcoderJob = new TranscoderJob
        {
            UserMetadata = new()
        };
        
        // Assert
        transcoderJob.GetAssetId().Should().BeNull();
    }
    
    [Theory]
    [InlineData("1/2/3/4/5")]
    [InlineData("foo")]
    [InlineData("foo/bar/baz")]
    public void GetAssetId_ReturnsNull_IfInvalidFormat(string input)
    {
        // Arrange
        var transcoderJob = new TranscoderJob
        {
            UserMetadata = new()
            {
                [TranscodeMetadataKeys.DlcsId] = input
            }
        };

        // Assert
        transcoderJob.GetAssetId().Should().BeNull();
    }

    [Fact]
    public void GetAssetId_ReturnsValue_IfFound()
    {
        // Arrange
        var transcoderJob = new TranscoderJob
        {
            UserMetadata = new()
            {
                [TranscodeMetadataKeys.DlcsId] = "1/2/foo"
            }
        };
        var expected = new AssetId(1, 2, "foo");

        // Assert
        transcoderJob.GetAssetId().Should().Be(expected);
    }
    
    [Fact]
    public void GetStoredOriginalAssetSize_Returns0_IfMissing()
    {
        // Arrange
        var transcoderJob = new TranscoderJob
        {
            UserMetadata = new()
        };
        
        // Assert
        transcoderJob.GetStoredOriginalAssetSize().Should().Be(0);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("foo", 0)]
    [InlineData("0", 0)]
    [InlineData("990", 990)]
    public void GetStoredOriginalAssetSize_Correct(string input, long expected)
    {
        // Arrange
        var transcoderJob = new TranscoderJob
        {
            UserMetadata = new()
            {
                [TranscodeMetadataKeys.OriginSize] = input
            }
        };

        // Assert
        transcoderJob.GetStoredOriginalAssetSize().Should().Be(expected);
    }
    
    [Fact]
    public void GetBatchId_ReturnsNull_IfMissing()
    {
        // Arrange
        var transcoderJob = new TranscoderJob
        {
            UserMetadata = new()
        };
        
        // Assert
        transcoderJob.GetBatchId().Should().BeNull();
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("foo")]
    public void GetBatchId_ReturnsNull_IfNotNumeric(string input)
    {
        // Arrange
        var transcoderJob = new TranscoderJob
        {
            UserMetadata = new()
            {
                [TranscodeMetadataKeys.BatchId] = input
            }
        };

        // Assert
        transcoderJob.GetBatchId().Should().BeNull();
    }
    
    [Theory]
    [InlineData("0", 0)]
    [InlineData("990", 990)]
    public void GetBatchId_ReturnsValue_IfNumeric(string input, int expected)
    {
        // Arrange
        var transcoderJob = new TranscoderJob
        {
            UserMetadata = new()
            {
                [TranscodeMetadataKeys.BatchId] = input
            }
        };

        // Assert
        transcoderJob.GetBatchId().Should().Be(expected);
    }
}
