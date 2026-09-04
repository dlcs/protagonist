using System;
using System.Net.Http;
using System.Threading.Tasks;
using DLCS.Repository.OriginStrategies;

namespace DLCS.Repository.Tests.OriginStrategies;

public class OriginConnectionGuardTests
{
    [Theory]
    [InlineData("http://127.0.0.1/foo")]
    [InlineData("http://127.1.2.3/foo")]
    [InlineData("https://127.0.0.1/foo")]
    [InlineData("http://[::1]/foo")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[fe80::1]/foo")]
    [InlineData("http://[fd00:ec2::254]/latest/meta-data")]
    [InlineData("http://localhost/foo")]
    public async Task ConnectAsync_Throws_IfHostIsAlwaysBlocked(string origin)
    {
        var httpClient = GetHttpClient();

        Func<Task> action = () => httpClient.GetAsync(origin);

        var exception = (await action.Should().ThrowAsync<HttpRequestException>()).Which;
        exception.InnerException.Should().BeOfType<OriginAddressBlockedException>();
    }

    [Fact]
    public async Task ConnectAsync_Throws_IfHostInConfiguredRange()
    {
        var httpClient = GetHttpClient("10.0.0.0/8");

        Func<Task> action = () => httpClient.GetAsync("http://10.1.2.3/foo");

        var exception = (await action.Should().ThrowAsync<HttpRequestException>()).Which;
        exception.InnerException.Should().BeOfType<OriginAddressBlockedException>();
    }

    private static HttpClient GetHttpClient(params string[] additionalBlockedRanges)
    {
        var guard = new OriginConnectionGuard(
            new OriginAddressPolicy(new OriginStrategySettings { BlockedIpRanges = additionalBlockedRanges }));

        // Exercise the guard as it's wired up in DI, so that HttpClient behaviour is included
        // UseProxy:false as a proxy would connect us to the proxy address, rather than the origin
        var handler = new SocketsHttpHandler { ConnectCallback = guard.ConnectAsync, UseProxy = false };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }
}
