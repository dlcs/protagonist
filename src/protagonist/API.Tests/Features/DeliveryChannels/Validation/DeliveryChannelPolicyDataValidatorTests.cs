using System.Threading.Tasks;
using API.Features.DeliveryChannels.Validation;
using DLCS.Model.DeliveryChannels;
using FakeItEasy;

namespace API.Tests.Features.DeliveryChannelPolicies.Validation;

public class DeliveryChannelPolicyDataValidatorTests
{
    private readonly DeliveryChannelPolicyDataValidator sut;
    private readonly string[] fakedAvPolicies =
    {
        "video-mp4-480p",
        "video-webm-720p",
        "audio-mp3-128k"
    };
    
    public DeliveryChannelPolicyDataValidatorTests()
    {
        var avChannelPolicyOptionsRepository = A.Fake<IAvChannelPolicyOptionsRepository>();
        A.CallTo(() => avChannelPolicyOptionsRepository.RetrieveAvChannelPolicyOptions())
            .Returns(fakedAvPolicies);
        sut = new DeliveryChannelPolicyDataValidator(avChannelPolicyOptionsRepository);
    }
    
    [Theory]
    [InlineData("[\"400,\",\"200,\",\"100,\"]")]
    [InlineData("[\"!400,\",\"!200,\",\"!100,\"]")]
    [InlineData("[\",400\",\",200\",\",100\"]")]
    [InlineData("[\"!,400\",\"!,200\",\"!,100\"]")]
    public async Task PolicyDataValidator_ReturnsTrue_ForValidThumbParameters(string policyData)
    {
        // Arrange and Act
        var result = await sut.Validate(policyData, "thumbs");
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public async Task PolicyDataValidator_ReturnsFalse_ForBadThumbSizes()
    {
        // Arrange
        var policyData = "[\"400,\",\"foo,bar\",\"100,\"]";
   
        // Act
        var result = await sut.Validate(policyData, "thumbs");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task PolicyDataValidator_ReturnsFalse_ForInvalidThumbSizesJson()
    {
        // Arrange
        var policyData = "[\"400,\",";
   
        // Act
        var result = await sut.Validate(policyData, "thumbs");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("[\"\"]")]
    public async Task PolicyDataValidator_ReturnsFalse_ForEmptyThumbSizes(string policyData)
    {
        // Arrange and Act
        var result = await sut.Validate(policyData, "thumbs");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("[\"max\"]")]
    [InlineData("[\"^max\"]")]
    [InlineData("[\"^,512\"]")]
    [InlineData("[\"^512,\"]")]
    [InlineData("[\"^!,512\"]")]
    [InlineData("[\"^!512,\"]")]
    [InlineData("[\"41.6,7.5\"]")]
    [InlineData("[\"^pct:41.6,7.5\"]")]
    [InlineData("[\"10,50\"]")]
    [InlineData("[\",\"]")]
    public async Task PolicyDataValidator_ReturnsFalse_ForInvalidThumbParameters(string policyData)
    {
        // Arrange and Act
        var result = await sut.Validate(policyData, "thumbs");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("[\"video-mp4-480p\"]")]
    [InlineData("[\"video-webm-720p\"]")]
    [InlineData("[\"audio-mp3-128k\"]")]
    public async Task PolicyDataValidator_ReturnsTrue_ForValidAvPolicy(string policyData)
    {
        // Arrange and Act
        var result = await sut.Validate(policyData, "iiif-av");
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public async Task PolicyDataValidator_ReturnsFalse_ForNonexistentAvPolicy()
    {
        // Arrange and Act
        var result = await sut.Validate("not-a-transcode-policy", "iiif-av");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("[\"\"]")]
    [InlineData("[\"policy-1\",\"\"]")]
    public async Task PolicyDataValidator_ReturnsFalse_ForBadAvPolicy(string policyData)
    {
        // Arrange and Act
        var result = await sut.Validate(policyData, "iiif-av");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("[\"\"]")]
    public async Task PolicyDataValidator_ReturnsFalse_ForEmptyAvPolicy(string policyData)
    {
        // Arrange and Act
        var result = await sut.Validate(policyData, "iiif-av");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task PolicyDataValidator_ReturnsFalse_ForInvalidAvPolicyJson()
    {
        // Arrange
        var policyData = "[\"policy-1\",";
        
        // Act
        var result = await sut.Validate(policyData, "iiif-av");
        
        // Assert
        result.Should().BeFalse();
    }
}