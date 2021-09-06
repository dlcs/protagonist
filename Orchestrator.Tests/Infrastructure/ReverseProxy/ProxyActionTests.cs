using FluentAssertions;
using Orchestrator.Infrastructure.ReverseProxy;
using Xunit;

namespace Orchestrator.Tests.Infrastructure.ReverseProxy
{
    public class ProxyActionTests
    {
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void HasPath_False_IfNullOrWhiteSpace(string path)
        {
            // Act
            var proxyAction = new ProxyActionResult(ProxyDestination.Thumbs, false, path);
            
            // Assert
            proxyAction.HasPath.Should().BeFalse();
        }

        [Theory]
        [InlineData("/this/is/the/way")]
        [InlineData("this/is/the/way")]
        public void Ctor_RemovesLeadingSlash(string path)
        {
            // Act
            var proxyAction = new ProxyActionResult(ProxyDestination.Thumbs, false, path);
            
            // Assert
            proxyAction.Path.Should().Be("this/is/the/way");
            proxyAction.HasPath.Should().BeTrue();
        }
    }
}