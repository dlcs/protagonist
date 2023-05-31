using DLCS.Web.Requests;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DLCS.Web.Tests.Requests;

public class HttpRequestXTests
{
    [Fact]
    public void GetFirstQueryParamValue_Finds_Single_Value()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?test=aaa&test2=bbb");
        
        // act
        var test = httpRequest.GetFirstQueryParamValue("test");
        
        // assert
        test.Should().Be("aaa");
    }
    
    [Fact]
    public void GetFirstQueryParamValue_Ignores_Further_Values()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?test=aaa&test=bbb&test2=ccc");
        
        // act
        var test = httpRequest.GetFirstQueryParamValue("test");
        
        // assert
        test.Should().Be("aaa");
    }
    
    [Fact]
    public void GetFirstQueryParamValueAsInt_Finds_Int()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?test=12");
        
        // act
        var test = httpRequest.GetFirstQueryParamValueAsInt("test");
        
        // assert
        test.Should().Be(12);
    }

    [Fact]
    public void GetDisplayUrl_ReturnsFullUrl_WhenCalledWithDefaultParams()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example?foo=bar";

        // Act
        var result = httpRequest.GetDisplayUrl();
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetDisplayUrl_WithPathBase_ReturnsFullUrl_WhenCalledWithDefaultParams()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.PathBase = new PathString("/v2");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example/v2?foo=bar";

        // Act
        var result = httpRequest.GetDisplayUrl();
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetDisplayUrl_ReturnsFullUrl_WhenCalledWithPath()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example/my-path/one/two?foo=bar";

        // Act
        var result = httpRequest.GetDisplayUrl("/my-path/one/two");
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetDisplayUrl_WithPathBase_ReturnsFullUrl_WhenCalledWithWithPath()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.PathBase = new PathString("/v2");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example/v2/my-path/one/two?foo=bar";

        // Act
        var result = httpRequest.GetDisplayUrl("/my-path/one/two");
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetDisplayUrl_ReturnsFullUrl_WithoutQueryParam_WhenCalledWithDoNotIncludeQueryParams()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example";

        // Act
        var result = httpRequest.GetDisplayUrl(includeQueryParams: false);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetDisplayUrl_WithPathBase_ReturnsFullUrl_WithoutQueryParam_WhenCalledWithDoNotIncludeQueryParams()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.PathBase = new PathString("/v2");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example/v2";

        // Act
        var result = httpRequest.GetDisplayUrl(includeQueryParams: false);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetBaseUrl_ReturnsFullUrl_WithoutQueryParam()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example";

        // Act
        var result = httpRequest.GetBaseUrl();
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetBaseUrl_WithPathBase_ReturnsFullUrl_WithoutQueryParam_WhenCalledWithDoNotIncludeQueryParams()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.PathBase = new PathString("/v2");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example/v2";

        // Act
        var result = httpRequest.GetBaseUrl();
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetJsonLdId_Correct()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example/anything";

        // Act
        var result = httpRequest.GetJsonLdId();
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetJsonLdId_WithPathBase_Correct()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.PathBase = new PathString("/v2");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example/v2/anything";

        // Act
        var result = httpRequest.GetJsonLdId();
        
        // Assert
        result.Should().Be(expected);
    }
}