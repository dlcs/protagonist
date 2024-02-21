using System.Linq;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Tests.Response;

public class HttpResponseXTests
{
    [Fact]
    public void SeeOther_SetsCorrectStatusCodeAndHeader()
    {
        // Arrange
        var httpResponse = new DefaultHttpContext().Response;
        
        // Act
        httpResponse.SeeOther("new-location");
        
        // Assert
        httpResponse.StatusCode.Should().Be(303);
        httpResponse.Headers.Should().ContainKey("Location")
            .WhoseValue.Single().Should().Be("new-location");
    }
    
    [Fact]
    public void SeeOther_SetsCorrectStatusCodeAndHeader_OverridingExistingLocationHeader()
    {
        // Arrange
        var httpResponse = new DefaultHttpContext().Response;
        httpResponse.Headers.Add("Location", "old-location");
        
        // Act
        httpResponse.SeeOther("new-location");
        
        // Assert
        httpResponse.StatusCode.Should().Be(303);
        httpResponse.Headers.Should().ContainKey("Location")
            .WhoseValue.Single().Should().Be("new-location");
    }
}