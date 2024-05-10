using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Orchestrator.Infrastructure.ReverseProxy;

namespace Orchestrator.Tests.Infrastructure.ReverseProxy;

public class PathRewriteTransformerTests
{
    [Theory]
    [InlineData("http://test.example.com")]
    [InlineData("http://test.example.com/")]
    public async Task TransformRequestAsync_SetRequestUri(string destination)
    {
        // Arrange
        var request = new HttpRequestMessage();
        var expected = new Uri("http://test.example.com/new/path");
        var actionResult = new ProxyActionResult(ProxyDestination.Unknown, true, "new/path");
        var sut = new PathRewriteTransformer(actionResult);

        // Act
        await sut.TransformRequestAsync(new DefaultHttpContext(), request, destination);
        
        // Assert
        request.RequestUri.Should().Be(expected);
    }
    
    [Fact]
    public async Task TransformRequestAsync_RewriteWholePathTrue_SetRequestUriToFullPath()
    {
        // Arrange
        var request = new HttpRequestMessage();
        var expected = new Uri("http://newtest.example.com/new/path");
        var actionResult =
            new ProxyActionResult(ProxyDestination.Unknown, true, "http://newtest.example.com/new/path");
        var sut = new PathRewriteTransformer(actionResult, true);

        // Act
        await sut.TransformRequestAsync(new DefaultHttpContext(), request, "http://test.example.com");
        
        // Assert
        request.RequestUri.Should().Be(expected);
    }
    
    [Fact]
    public async Task TransformRequestAsync_RewriteWholePathTrue_SetsHostHeader()
    {
        // Arrange
        var request = new HttpRequestMessage();
        var actionResult =
            new ProxyActionResult(ProxyDestination.Unknown, true, "http://newtest.example.com/new/path");
        var sut = new PathRewriteTransformer(actionResult, true);

        // Act
        await sut.TransformRequestAsync(new DefaultHttpContext(), request, "http://test.example.com");
        
        // Assert
        request.Headers.Host.Should().Be("newtest.example.com");
    }
    
    [Fact]
    public async Task TransformRequestAsync_RewriteWholePathFalse_SetsHostHeader()
    {
        // Arrange
        var request = new HttpRequestMessage();
        var actionResult = new ProxyActionResult(ProxyDestination.Unknown, true, "new/path");
        var sut = new PathRewriteTransformer(actionResult);

        // Act
        await sut.TransformRequestAsync(new DefaultHttpContext(), request, "http://test.example.com");
        
        // Assert
        request.Headers.Host.Should().Be("test.example.com");
    }
    
    [Theory]
    [InlineData("http://newtest.example.com/new/path", true)]
    [InlineData("new/path", false)]
    public async Task TransformRequestAsync_SetsRequestedByHeader(string path, bool rewriteWholePath)
    {
        // Arrange
        var request = new HttpRequestMessage();
        var actionResult = new ProxyActionResult(ProxyDestination.Unknown, true, path);
        var sut = new PathRewriteTransformer(actionResult, rewriteWholePath);

        // Act
        await sut.TransformRequestAsync(new DefaultHttpContext(), request, "http://test.example.com");
        
        // Assert
        request.Headers.Should().ContainKey("x-requested-by");
    }

    [Fact]
    public async Task TransformRequestAsync_RemovesCloudfrontIdHeaderIfPresent()
    {
        // Arrange
        var request = new HttpRequestMessage();
        request.Headers.Add("x-amz-cf-id", "foo");
        var actionResult = new ProxyActionResult(ProxyDestination.S3, true, "new/path");
        var sut = new PathRewriteTransformer(actionResult, false);

        // Act
        await sut.TransformRequestAsync(new DefaultHttpContext(), request, "http://test.example.com");
        
        // Assert
        request.Headers.Should().NotContainKey("x-amz-cf-id");
    }

    [Fact]
    public async Task TransformResponseAsync_AddsCORSHeader_IfMissing()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var responseMessage = new HttpResponseMessage();
        var actionResult = new ProxyActionResult(ProxyDestination.Unknown, true, "whatever");
        var sut = new PathRewriteTransformer(actionResult);
        
        // Act
        await sut.TransformResponseAsync(context, responseMessage);
        
        // Assert
        context.Response.Headers.Should().ContainKey("Access-Control-Allow-Origin")
            .WhoseValue.Single().Should().Be("*");
    }
    
    [Fact]
    public async Task TransformResponseAsync_DoesNotChangeCORSHeader_IfAlreadyInResponseFromDownstream()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var responseMessage = new HttpResponseMessage();
        responseMessage.Headers.Add("Access-Control-Allow-Origin", "_value_");
        var actionResult = new ProxyActionResult(ProxyDestination.Unknown, true, "whatever");
        var sut = new PathRewriteTransformer(actionResult);
        
        // Act
        await sut.TransformResponseAsync(context, responseMessage);
        
        // Assert
        context.Response.Headers.Should().ContainKey("Access-Control-Allow-Origin")
            .WhoseValue.Single().Should().Be("_value_");
    }
    
    [Fact]
    public async Task TransformResponseAsync_AddsCacheHeaders_IfImageServer_AndPublic()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var responseMessage = new HttpResponseMessage();
        responseMessage.Headers.Add("Cache-Control", "max-age=200");
        var actionResult = new ProxyActionResult(ProxyDestination.ImageServer, false, "whatever");
        var sut = new PathRewriteTransformer(actionResult);
        
        // Act
        await sut.TransformResponseAsync(context, responseMessage);
        
        // Assert
        context.Response.Headers.Should().ContainKey("Cache-Control")
            .WhoseValue.Single().Should().Be("public, s-maxage=2419200, max-age=2419200, stale-if-error=86400");
    }
    
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task TransformResponseAsync_AddsSmallMaxAge_RegardlessOfDestination_IfError(HttpStatusCode statusCode)
    {
        // Arrange
        var context = new DefaultHttpContext();
        var responseMessage = new HttpResponseMessage { StatusCode = statusCode };
        responseMessage.Headers.Add("Cache-Control", "max-age=200");
        var actionResult = new ProxyActionResult(ProxyDestination.S3, false, "whatever");
        var sut = new PathRewriteTransformer(actionResult);
        
        // Act
        await sut.TransformResponseAsync(context, responseMessage);
        
        // Assert
        context.Response.Headers.Should().ContainKey("Cache-Control")
            .WhoseValue.Single().Should().Be("max-age=60");
    }
    
    [Fact]
    public async Task TransformResponseAsync_AddsPrivateCacheHeaders_IfImageServer_AndRequiresAuth()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var responseMessage = new HttpResponseMessage();
        responseMessage.Headers.Add("Cache-Control", "max-age=200");
        var actionResult = new ProxyActionResult(ProxyDestination.ImageServer, true, "whatever");
        var sut = new PathRewriteTransformer(actionResult);
        
        // Act
        await sut.TransformResponseAsync(context, responseMessage);
        
        // Assert
        context.Response.Headers.Should().ContainKey("Cache-Control")
            .WhoseValue.Single().Should().Be("private, max-age=600");
    }
    
    [Fact]
    public async Task TransformResponseAsync_SetsCustomHeaders_BasedOnHeadersProp()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var responseMessage = new HttpResponseMessage();
        responseMessage.Headers.Add("Cache-Control", "max-age=200");
        var actionResult = new ProxyActionResult(ProxyDestination.ImageServer, true, "whatever");
        actionResult.WithHeader("x-test-header", "live forever")
            .WithHeader("Cache-Control", "max-age=999");
        var sut = new PathRewriteTransformer(actionResult);
        
        // Act
        await sut.TransformResponseAsync(context, responseMessage);
        
        // Assert
        context.Response.Headers.Should().ContainKey("Cache-Control")
            .WhoseValue.Single().Should().Be("max-age=999");
        context.Response.Headers.Should().ContainKey("x-test-header")
            .WhoseValue.Single().Should().Be("live forever");
    }
    
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task TransformResponseAsync_DoesNotSetCustomHeaders_IfError(HttpStatusCode statusCode)
    {
        // Arrange
        var context = new DefaultHttpContext();
        var responseMessage = new HttpResponseMessage { StatusCode = statusCode };
        responseMessage.Headers.Add("Cache-Control", "max-age=200");
        var actionResult = new ProxyActionResult(ProxyDestination.ImageServer, true, "whatever");
        actionResult.WithHeader("x-test-header", "live forever")
            .WithHeader("Cache-Control", "max-age=999");
        var sut = new PathRewriteTransformer(actionResult);
        
        // Act
        await sut.TransformResponseAsync(context, responseMessage);
        
        // Assert
        context.Response.Headers.Should().ContainKey("Cache-Control")
            .WhoseValue.Single().Should().Be("max-age=60");
        context.Response.Headers.Should().NotContainKey("x-test-header");
    }
}