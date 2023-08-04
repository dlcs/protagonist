using IIIF.Auth.V2;
using Orchestrator.Infrastructure.Auth.V2;

namespace Orchestrator.Tests.Infrastructure.Auth.V2;

public class IIIFAuth2ParsingHelpersTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetAccessServiceNameFromDefaultPath_ReturnsNullOrEmpty_IfIdNullOrEmpty(string id)
    {
        // Arrange
        var tokenService = new AuthAccessService2 { Id = id };
        
        // Act
        var accessServiceName = tokenService.GetAccessServiceNameFromDefaultPath();
        
        // Assert
        accessServiceName.Should().BeNullOrEmpty();
    }
    
    [Fact]
    public void GetAccessServiceNameFromDefaultPath_ReturnsCorrectAccessServiceName()
    {
        // Arrange
        var tokenService = new AuthAccessService2 { Id = "https://dlcs.digirati.io/auth/v2/access/2/clickthrough" };
        
        // Act
        var accessServiceName = tokenService.GetAccessServiceNameFromDefaultPath();
        
        // Assert
        accessServiceName.Should().Be("clickthrough", "the last path part is access-service-name");
    }
}