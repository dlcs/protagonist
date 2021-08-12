using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Orchestrator.ReverseProxy;
using Xunit;

namespace Orchestrator.Tests.ReverseProxy
{
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
            var sut = new PathRewriteTransformer("new/path");

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
            var sut = new PathRewriteTransformer("http://newtest.example.com/new/path", true);

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
            var sut = new PathRewriteTransformer("http://newtest.example.com/new/path", true);

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
            var expected = new Uri("http://test.example.com/new/path");
            var sut = new PathRewriteTransformer("new/path");

            // Act
            await sut.TransformRequestAsync(new DefaultHttpContext(), request, "http://test.example.com");
            
            // Assert
            request.Headers.Host.Should().Be("test.example.com");
        }
    }
}