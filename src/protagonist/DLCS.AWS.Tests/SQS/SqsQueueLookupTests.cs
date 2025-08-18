using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
using DLCS.Model.Assets;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Tests.SQS;

public class SqsQueueLookupTests
{
    private readonly SqsQueueLookup sut;

    public SqsQueueLookupTests()
    {
        sut = new SqsQueueLookup(Options.Create(new AWSSettings
        {
            Region = "eu-west-1",
            SQS = new SQSSettings
            {
                PriorityImageQueueName = "test-priority",
                ImageQueueName = "test-image",
                TimebasedQueueName = "test-timebased",
                FileQueueName = "test-file"
            }
        }));
    }
    
    [Fact]
    public void GetQueueNameForFamily_Image_Correct()
    {
        // Act
        var result = sut.GetQueueNameForFamily(AssetFamily.Image);
        
        // Assert
        result.Should().Be("test-image");
    }
    
    [Fact]
    public void GetQueueNameForFamily_Image_Priority_Correct()
    {
        // Act
        var result = sut.GetQueueNameForFamily(AssetFamily.Image, true);
        
        // Assert
        result.Should().Be("test-priority");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetQueueNameForFamily_Timebased_Correct_IgnoresPriority(bool priority)
    {
        // Act
        var result = sut.GetQueueNameForFamily(AssetFamily.Timebased, priority);
        
        // Assert
        result.Should().Be("test-timebased");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetQueueNameForFamily_File_Correct_IgnoresPriority(bool priority)
    {
        // Act
        var result = sut.GetQueueNameForFamily(AssetFamily.File, priority);
        
        // Assert
        result.Should().Be("test-file");
    }
}