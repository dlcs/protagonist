using DLCS.HydraModel;
using FluentAssertions;
using Xunit;

namespace API.Tests.JsonLd;

public class ApiKeyTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Key_Null_IfIdNullOrEmpty(string id)
    {
        // Arrange
        var apiKey = new ApiKey {Id = id};
        
        // Assert
        apiKey.Key.Should().BeNull();
    }
    
    [Fact]
    public void Key_Null_IfIdHasNoSlashes()
    {
        // Arrange
        var apiKey = new ApiKey {Id = "unexpected value"};
        
        // Assert
        apiKey.Key.Should().BeNull();
    }
    
    [Fact]
    public void Key_ReturnsValue_FromId()
    {
        // Arrange
        var apiKey = new ApiKey
        {
            Id = "https://api.dlcs.digirati.io/customers/2/keys/25e6018f-67ae-4cdc-bdd2-ab22148dedb8"
        };
        
        // Assert
        apiKey.Key.Should().Be("25e6018f-67ae-4cdc-bdd2-ab22148dedb8");
    }
}