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
        private readonly PathRewriteTransformer sut;

        public PathRewriteTransformerTests()
        {
            sut = new PathRewriteTransformer("new/path");
        }

        [Fact]
        public async Task TransformRequestAsync_SetRequestUri()
        {
            // Arrange
            var request = new HttpRequestMessage();
            var expected = new Uri("http://test.example.com/new/path");

            // Act
            await sut.TransformRequestAsync(new DefaultHttpContext(), request, "http://test.example.com");
            
            // Assert
            request.RequestUri.Should().Be(expected);
        }
    }
}