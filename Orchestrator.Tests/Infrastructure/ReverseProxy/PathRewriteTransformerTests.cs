using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Orchestrator.Infrastructure.ReverseProxy;
using Xunit;

namespace Orchestrator.Tests.Infrastructure.ReverseProxy
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
            var sut = new PathRewriteTransformer("new/path");

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
            var sut = new PathRewriteTransformer(path, rewriteWholePath);

            // Act
            await sut.TransformRequestAsync(new DefaultHttpContext(), request, "http://test.example.com");
            
            // Assert
            request.Headers.Should().ContainKey("x-requested-by");
        }

        [Fact]
        public async Task TransformResponseAsync_AddsCORSHeader_IfMissing()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var responseMessage = new HttpResponseMessage();
            var sut = new PathRewriteTransformer("whatever");
            
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
            var sut = new PathRewriteTransformer("whatever");
            
            // Act
            await sut.TransformResponseAsync(context, responseMessage);
            
            // Assert
            context.Response.Headers.Should().ContainKey("Access-Control-Allow-Origin")
                .WhoseValue.Single().Should().Be("_value_");
        }
    }
}