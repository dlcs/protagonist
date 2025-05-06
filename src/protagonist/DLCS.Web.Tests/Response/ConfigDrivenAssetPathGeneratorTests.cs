using System;
using System.Collections.Generic;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DLCS.Web.Tests.Response;

public class ConfigDrivenAssetPathGeneratorTests
{
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetFullPathForRequest_Default(string customerPathValue)
    {
        // Arrange
        var sut = GetSut("default.com");
        var request = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(123, "test-customer"),
            CustomerPathValue = customerPathValue,
            Space = 10,
            AssetPath = "path/to/asset",
            BasePath = "thumbs/123/10",
            RoutePrefix = "thumbs"
        };

        var expected = $"https://default.com/thumbs/{customerPathValue}/10/path/to/asset";
        
        // Act
        var actual = sut.GetFullPathForRequest(request);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetFullPathForRequest_Override(string customerPathValue)
    {
        // Arrange
        var sut = GetSut("test.example.com");
        var request = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(123, "test-customer"),
            CustomerPathValue = customerPathValue,
            Space = 10,
            AssetPath = "path/to/asset",
            BasePath = "thumbs/123/10",
            RoutePrefix = "thumbs"
        };

        var expected = "https://test.example.com/thumbs/path/to/asset";
        
        // Act
        var actual = sut.GetFullPathForRequest(request);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetFullPathForRequest_Override_WithPrefixReplacement(string customerPathValue)
    {
        // Arrange
        var sut = GetSut("other.example.com");
        var request = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(123, "test-customer"),
            CustomerPathValue = customerPathValue,
            Space = 10,
            AssetPath = "path/to/asset",
            BasePath = "thumbs/123/10",
            RoutePrefix = "iiif-img"
        };

        var expected = "https://other.example.com/foo/img/path/to/asset";
        
        // Act
        var actual = sut.GetFullPathForRequest(request);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetFullPathForRequest_OverrideAvailable_ButIgnoredIfNativeFormatRequested(string customerPathValue)
    {
        // Arrange
        var sut = GetSut("test.example.com");
        var request = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(123, "test-customer"),
            CustomerPathValue = customerPathValue,
            Space = 10,
            AssetPath = "path/to/asset",
            BasePath = "thumbs/123/10",
            RoutePrefix = "thumbs"
        };

        var expected = $"https://test.example.com/thumbs/{customerPathValue}/10/path/to/asset";
        
        // Act
        var actual = sut.GetFullPathForRequest(request, true);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetFullPathForRequest_Default_QueryParam(string customerPathValue)
    {
        // Arrange
        var sut = GetSut("default.com", req => req.QueryString = new QueryString("?foo=bar"));
        var request = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(123, "test-customer"),
            CustomerPathValue = customerPathValue,
            Space = 10,
            AssetPath = "path/to/asset",
            BasePath = "thumbs/123/10",
            RoutePrefix = "thumbs"
        };

        var expected = $"https://default.com/thumbs/{customerPathValue}/10/path/to/asset?foo=bar";
        
        // Act
        var actual = sut.GetFullPathForRequest(request);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetFullPathForRequest_Default_QueryParam_OmittedIfIncludeFalse(string customerPathValue)
    {
        // Arrange
        var sut = GetSut("default.com", req => req.QueryString = new QueryString("?foo=bar"));
        var request = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(123, "test-customer"),
            CustomerPathValue = customerPathValue,
            Space = 10,
            AssetPath = "path/to/asset",
            BasePath = "thumbs/123/10",
            RoutePrefix = "thumbs"
        };

        var expected = $"https://default.com/thumbs/{customerPathValue}/10/path/to/asset";
        
        // Act
        var actual = sut.GetFullPathForRequest(request, includeQueryParams: false);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetRelativePathForRequest_Default(string customerPathValue)
    {
        // Arrange
        var sut = GetSut("default.com");
        var request = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(123, "test-customer"),
            CustomerPathValue = customerPathValue,
            Space = 10,
            AssetPath = "path/to/asset",
            BasePath = "thumbs/123/10",
            RoutePrefix = "thumbs"
        };

        var expected = $"/thumbs/{customerPathValue}/10/path/to/asset";
        
        // Act
        var actual = sut.GetRelativePathForRequest(request);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetRelativePathForRequest_Override(string customerPathValue)
    {
        // Arrange
        var sut = GetSut("test.example.com");
        var request = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(123, "test-customer"),
            CustomerPathValue = customerPathValue,
            Space = 10,
            AssetPath = "path/to/asset",
            BasePath = "thumbs/123/10",
            RoutePrefix = "thumbs"
        };

        var expected = "/thumbs/path/to/asset";
        
        // Act
        var actual = sut.GetRelativePathForRequest(request);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetRelativePathForRequest_Override_WithPrefixReplacement(string customerPathValue)
    {
        // Arrange
        var sut = GetSut("other.example.com");
        var request = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(123, "test-customer"),
            CustomerPathValue = customerPathValue,
            Space = 10,
            AssetPath = "path/to/asset",
            BasePath = "thumbs/123/10",
            RoutePrefix = "iiif-img"
        };

        var expected = "/foo/img/path/to/asset";
        
        // Act
        var actual = sut.GetRelativePathForRequest(request);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetRelativePathForRequest_OverrideAvailable_ButIgnoredIfNativeFormatRequested(string customerPathValue)
    {
        // Arrange
        var sut = GetSut("test.example.com");
        var request = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(123, "test-customer"),
            CustomerPathValue = customerPathValue,
            Space = 10,
            AssetPath = "path/to/asset",
            BasePath = "thumbs/123/10",
            RoutePrefix = "thumbs"
        };

        var expected = $"/thumbs/{customerPathValue}/10/path/to/asset";
        
        // Act
        var actual = sut.GetRelativePathForRequest(request, true);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetRelativePathForRequest_Default_QueryParamsAreRemoved(string customerPathValue)
    {
        // Arrange
        var sut = GetSut("default.com", req => req.QueryString = new QueryString("?foo=bar"));
        var request = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(123, "test-customer"),
            CustomerPathValue = customerPathValue,
            Space = 10,
            AssetPath = "path/to/asset",
            BasePath = "thumbs/123/10",
            RoutePrefix = "thumbs"
        };

        var expected = $"/thumbs/{customerPathValue}/10/path/to/asset";
        
        // Act
        var actual = sut.GetRelativePathForRequest(request);
        
        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void PathHasVersion_True_ForFallbackPath() 
        => GetSut("default.com").PathHasVersion().Should().BeTrue("Default native path contains {version}");

    [Fact]
    public void PathHasVersion_True_IfHostSpecificHasVersion()
        => GetSut("versioned.example.com").PathHasVersion().Should().BeTrue("Host template contains {version}");
    
    [Theory]
    [InlineData("test.example.com")]
    [InlineData("other.example.com")]
    public void PathHasVersion_False_IfHostSpecificHasNoVersion(string hostname) 
        => GetSut(hostname).PathHasVersion().Should().BeFalse("Host template contains {version}");

    private static ConfigDrivenAssetPathGenerator GetSut(string host, Action<HttpRequest> requestModifier = null)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        var contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(context);
        request.Host = new HostString(host);
        request.Scheme = "https";
        requestModifier?.Invoke(request);

        var options = Options.Create(new PathTemplateOptions
        {
            Overrides = new Dictionary<string, PathTemplate>
            {
                ["test.example.com"] = new() { Path = "/{prefix}/{assetPath}" },
                ["other.example.com"] = new()
                {
                    Path = "/foo/{prefix}/{assetPath}", PrefixReplacements = new Dictionary<string, string>
                    {
                        ["iiif-img"] = "img",
                    }
                },
                ["versioned.example.com"] = new() { Path = "/{prefix}/{version}_{assetPath}" },
            }
        });

        return new ConfigDrivenAssetPathGenerator(options, contextAccessor);
    }
}
