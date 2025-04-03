using System;
using DLCS.Web.Auth;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Tests.Auth;

public class AuthenticationHeaderUtilsTests
{
    public const string Bearer = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1lIjoiSm9obiBEb2UifQ";
    public const string Basic = "Basic Zm9vOmJhcg==";
    
    [Fact]
    public void GetAuthHeaderValue_Null_IfNoAuthHeader()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        
        // Act
        var authHeadervalue = httpRequest.GetAuthHeaderValue();
        
        // Assert
        authHeadervalue.Should().BeNull();
    }
    
    [Theory]
    [InlineData(Basic)]
    [InlineData(Bearer)]
    public void GetAuthHeaderValue_ReturnsHeader_IfNoSchemeSpecified(string authHeader)
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Append("Authorization", authHeader);

        var parts = authHeader.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        var scheme = parts[0];
        var parameter = parts[1];
        
        // Act
        var authHeadervalue = httpRequest.GetAuthHeaderValue();
        
        // Assert
        authHeadervalue.Scheme.Should().Be(scheme);
        authHeadervalue.Parameter.Should().Be(parameter);
    }
    
    [Theory]
    [InlineData(Basic, "Bearer")]
    [InlineData(Bearer, "Basic")]
    public void GetAuthHeaderValue_FiltersOutDifferentScheme_IfSpecified(string authHeader, string targetScheme)
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Append("Authorization", authHeader);
        
        // Act
        var authHeadervalue = httpRequest.GetAuthHeaderValue(targetScheme);
        
        // Assert
        authHeadervalue.Should().BeNull();
    }
    
    [Theory]
    [InlineData(Basic, "Basic")]
    [InlineData(Bearer, "Bearer")]
    [InlineData(Basic, "basic")]
    [InlineData(Bearer, "bearer")]
    public void GetAuthHeaderValue_ReturnsHeader_IfMatchingSchemeSpecified(string authHeader, string targetScheme)
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Append("Authorization", authHeader);

        var parameter = authHeader.Split(" ", StringSplitOptions.RemoveEmptyEntries)[1];

        // Act
        var authHeadervalue = httpRequest.GetAuthHeaderValue(targetScheme);
        
        // Assert
        authHeadervalue.Scheme.Should().BeEquivalentTo(targetScheme);
        authHeadervalue.Parameter.Should().Be(parameter);
    }
}
