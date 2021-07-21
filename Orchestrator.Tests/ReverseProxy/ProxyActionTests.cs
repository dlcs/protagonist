using FluentAssertions;
using Orchestrator.ReverseProxy;
using Xunit;

namespace Orchestrator.Tests.ReverseProxy
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
            var proxyAction = new ProxyAction(ProxyTo.Thumbs, path);
            
            // Assert
            proxyAction.HasPath.Should().BeFalse();
        }
    }
}