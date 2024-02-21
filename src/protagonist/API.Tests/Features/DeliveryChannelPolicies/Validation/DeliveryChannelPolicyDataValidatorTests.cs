using API.Features.DeliveryChannelPolicies.Validation;

namespace API.Tests.Features.DeliveryChannelPolicies.Validation;

public class DeliveryChannelPolicyDataValidatorTests
{
    private readonly DeliveryChannelPolicyDataValidator sut;
    
    public DeliveryChannelPolicyDataValidatorTests()
    {
        sut = new DeliveryChannelPolicyDataValidator();
    }
    
    [Theory]
    [InlineData("[\"400,400\",\"200,200\",\"100,100\"]")]
    [InlineData("[\"!400,400\",\"!200,200\",\"!100,100\"]")]
    [InlineData("[\"400,\",\"200,\",\"100,\"]")]
    [InlineData("[\"!400,\",\"!200,\",\"!100,\"]")]
    [InlineData("[\"400,400\"]")]
    public void PolicyDataValidator_ReturnsTrue_ForValidThumbSizes(string policyData)
    {
        // Arrange And Act
        var result = sut.Validate(policyData, "thumbs");
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void PolicyDataValidator_ReturnsFalse_ForBadThumbSizes()
    {
        // Arrange
        var policyData = "[\"400,400\",\"foo,bar\",\"100,100\"]";
   
        // Act
        var result = sut.Validate(policyData, "thumbs");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void PolicyDataValidator_ReturnsFalse_ForInvalidThumbSizesJson()
    {
        // Arrange
        var policyData = "[\"400,400\",";
   
        // Act
        var result = sut.Validate(policyData, "thumbs");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("[\"\"]")]
    public void PolicyDataValidator_ReturnsFalse_ForEmptyThumbSizes(string policyData)
    {
        // Arrange and Act
        var result = sut.Validate(policyData, "thumbs");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void PolicyDataValidator_ReturnsTrue_ForValidAvPolicy()
    {
        // Arrange
        var policyData = "[\"media-format-quality\"]"; // For now, any single string value is accepted - this will need
                                                       // to be rewritten once the API requires a valid transcoder policy
                                                       
        // Act
        var result = sut.Validate(policyData, "iiif-av");
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void PolicyDataValidator_ReturnsFalse_ForBadAvPolicy()
    {
        // Arrange
        var policyData = "[\"policy-1\",\"policy-2\"]";
        
        // Arrange and Act
        var result = sut.Validate(policyData, "iiif-av");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("[\"\"]")]
    public void PolicyDataValidator_ReturnsFalse_ForEmptyAvPolicy(string policyData)
    {
        // Arrange and Act
        var result = sut.Validate(policyData, "iiif-av");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void PolicyDataValidator_ReturnsFalse_ForInvalidAvPolicyJson()
    {
        // Arrange
        var policyData = "[\"policy-1\",";
        
        // Act
        var result = sut.Validate(policyData, "iiif-av");
        
        // Assert
        result.Should().BeFalse();
    }
}