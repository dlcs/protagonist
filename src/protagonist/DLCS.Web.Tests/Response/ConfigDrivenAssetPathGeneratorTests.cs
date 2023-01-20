using System.Collections.Generic;
using DLCS.Core;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

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
    public void GetFullPathForRequest_PathGenerator_Default(string customerPathValue)
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

        var expected = $"https://default.com/thumbs/{customerPathValue}/2000/not-asset";
        
        // Act
        var actual = sut.GetFullPathForRequest(request,
            (assetRequest, template) =>
                DlcsPathHelpers.GeneratePathFromTemplate(
                    template, 
                    assetRequest.RoutePrefix, 
                    assetRequest.CustomerPathValue, 
                    space: "2000",
                    assetPath: "not-asset"));
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetFullPathForRequest_PathGenerator_Override(string customerPathValue)
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

        var expected = "https://test.example.com/thumbs/not-asset";
        
        // Act
        var actual = sut.GetFullPathForRequest(request,
            (assetRequest, template) =>
                DlcsPathHelpers.GeneratePathFromTemplate(
                    template, 
                    assetRequest.RoutePrefix, 
                    assetRequest.CustomerPathValue, 
                    space: "2000",
                    assetPath: "not-asset"));
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("test-customer")]
    public void GetFullPathForRequest_PathGenerator_OverrideAvailable_ButIgnoredIfNativeFormatRequested(string customerPathValue)
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

        var expected = $"https://test.example.com/thumbs/{customerPathValue}/2000/not-asset";

        // Act
        var actual = sut.GetFullPathForRequest(request,
            (assetRequest, template) =>
                DlcsPathHelpers.GeneratePathFromTemplate(
                    template, 
                    assetRequest.RoutePrefix, 
                    assetRequest.CustomerPathValue, 
                    space: "2000",
                    assetPath: "not-asset"),
            true);
        
        // Assert
        actual.Should().Be(expected);
    }

    private ConfigDrivenAssetPathGenerator GetSut(string host)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        var contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(context);
        request.Host = new HostString(host);
        request.Scheme = "https";

        var options = Options.Create(new PathTemplateOptions
        {
            Overrides = new Dictionary<string, string>
            {
                ["test.example.com"] = "/{prefix}/{assetPath}"
            }
        });

        return new ConfigDrivenAssetPathGenerator(options, contextAccessor);
    }
}