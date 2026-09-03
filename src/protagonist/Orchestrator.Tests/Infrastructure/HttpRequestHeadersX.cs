using System.Net.Http;
using Orchestrator.Infrastructure;

namespace Orchestrator.Tests.Infrastructure;

public class HttpRequestHeadersX
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void WithGatewayToken_Ignored_IfNullOrWhitespace(string header)
    {
        var request = new HttpRequestMessage();
        request.Headers.WithGatewayToken(header);
        request.Headers.Should().BeEmpty("No header added");
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void WithGatewayToken_RemovesProvidedTokenHeader_IfNullOrWhitespace(string header)
    {
        var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation("x-gateway-token", "abc123");
        request.Headers.WithGatewayToken(header);
        request.Headers.Should().BeEmpty("Supplied header removed");
    }
    
    [Fact]
    public void WithGatewayToken_AddsToken()
    {
        var request = new HttpRequestMessage();
        request.Headers.WithGatewayToken("xyz999");
        request.Headers.Should().ContainKey("x-gateway-token")
            .WhoseValue.Should().ContainSingle("xyz999");
    }
    
    [Fact]
    public void WithGatewayToken_ReplacesProvidedTokenHeader()
    {
        var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation("x-gateway-token", "abc123");
        request.Headers.WithGatewayToken("xyz999");
        request.Headers.Should().ContainKey("x-gateway-token")
            .WhoseValue.Should().ContainSingle("xyz999");
    }
}
