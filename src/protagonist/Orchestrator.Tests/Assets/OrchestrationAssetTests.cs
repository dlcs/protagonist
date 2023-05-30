using Orchestrator.Assets;

namespace Orchestrator.Tests.Assets;

public class OrchestrationAssetTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsNotFound_True_IfS3LocationEmpty_AndReingestFalse(string s3Location)
    {
        // Arrange
        var orchestrationAsset = new OrchestrationImage { Reingest = false, S3Location = s3Location };
        
        // Assert
        orchestrationAsset.IsNotFound().Should().BeTrue();
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsNotFound_False_IfS3LocationEmpty_ButReingestTrue(string s3Location)
    {
        // Arrange
        var orchestrationAsset = new OrchestrationImage { Reingest = true, S3Location = s3Location };
        
        // Assert
        orchestrationAsset.IsNotFound().Should().BeFalse();
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsNotFound_False_IfS3LocationHasValue(bool reingest)
    {
        // Arrange
        var orchestrationAsset = new OrchestrationImage { Reingest = reingest, S3Location = "s3://" };
        
        // Assert
        orchestrationAsset.IsNotFound().Should().BeFalse();
    }
}