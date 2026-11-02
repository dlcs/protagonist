using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Settings;

namespace Orchestrator.Tests.Infrastructure.Auth;

public class ClientIpResolverTests
{
    private const string Secret = "cdn-secret";
    private const string OriginVerifyHeader = "X-Origin-Verify";
    private static readonly IPAddress LoadBalancerIp = IPAddress.Parse("10.1.0.10");

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("10.0.0.5, 198.51.100.9", "198.51.100.9")]
    [InlineData("10.0.0.5, 203.0.113.7, 130.176.0.1", "130.176.0.1")]
    public async Task GetClientIp_ReturnsRightmost_IfNoSecretsConfigured(string forwardedFor, string expected)
    {
        var (sut, _) = await ProcessRequest(forwardedFor, originVerify: Secret, secrets: []);

        sut.GetClientIp().Should().Be(IPAddress.Parse(expected));
    }

    [Fact]
    public async Task GetClientIp_Returns2ndFromRight_IfValidSecret()
    {
        var (sut, _) = await ProcessRequest("10.0.0.5, 203.0.113.7, 130.176.0.1", originVerify: Secret);

        sut.GetClientIp().Should().Be(IPAddress.Parse("203.0.113.7"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-the-secret")]
    [InlineData("cdn-secret-")]
    public async Task GetClientIp_ReturnsRightmost_IfSecretMissingOrInvalid(string? originVerify)
    {
        // Direct to load balancer with spoofed XFF entry, rightmost is the real caller
        var (sut, _) = await ProcessRequest("10.0.0.5, 198.51.100.9", originVerify: originVerify);

        sut.GetClientIp().Should().Be(IPAddress.Parse("198.51.100.9"));
    }

    [Fact]
    public async Task GetClientIp_ReturnsRightmost_IfMultipleOriginVerifyValues()
    {
        var (sut, _) = await ProcessRequest("10.0.0.5, 198.51.100.9", originVerify: [Secret, Secret]);

        sut.GetClientIp().Should().Be(IPAddress.Parse("198.51.100.9"));
    }

    [Theory]
    [InlineData("old-secret")]
    [InlineData("new-secret")]
    public async Task GetClientIp_AcceptsAnyConfiguredSecret(string originVerify)
    {
        var (sut, _) = await ProcessRequest("10.0.0.5, 203.0.113.7, 130.176.0.1", originVerify: originVerify,
            secrets: ["old-secret", "new-secret"]);

        sut.GetClientIp().Should().Be(IPAddress.Parse("203.0.113.7"));
    }

    [Fact]
    public async Task GetClientIp_ReturnsNull_IfValidSecret_ButOnlyOneEntry()
    {
        var (sut, _) = await ProcessRequest("130.176.0.1", originVerify: Secret);

        sut.GetClientIp().Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetClientIp_ReturnsNull_IfNoForwardedFor(string? forwardedFor)
    {
        var (sut, _) = await ProcessRequest(forwardedFor);

        sut.GetClientIp().Should().BeNull();
    }

    [Fact]
    public async Task GetClientIp_ReturnsNull_IfCallerNotKnownProxy()
    {
        var (sut, _) = await ProcessRequest("203.0.113.7", knownNetwork: "192.168.0.0/16");

        sut.GetClientIp().Should().BeNull();
    }

    [Fact]
    public async Task GetClientIp_ReturnsClientIp_IfCallerInKnownNetworks()
    {
        var (sut, _) = await ProcessRequest("10.0.0.5, 203.0.113.7, 130.176.0.1", originVerify: Secret,
            knownNetwork: "10.1.0.0/16");

        sut.GetClientIp().Should().Be(IPAddress.Parse("203.0.113.7"));
    }

    [Fact]
    public async Task GetClientIp_ReturnsClientIp_IfNoCallerIp()
    {
        // e.g. TestServer, matches forwarded headers middleware behaviour
        var (sut, _) = await ProcessRequest("203.0.113.7", knownNetwork: "10.1.0.0/16", callerIp: null);

        sut.GetClientIp().Should().Be(IPAddress.Parse("203.0.113.7"));
    }

    [Theory]
    [InlineData("2001:db8:1007::1", "2001:db8:1007::1")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("203.0.113.7:51234", "203.0.113.7")]
    [InlineData("[2001:db8:1007::1]:51234", "2001:db8:1007::1")]
    public async Task GetClientIp_ParsesAndNormalisesEntry(string entry, string expected)
    {
        var (sut, _) = await ProcessRequest(entry);

        sut.GetClientIp().Should().Be(IPAddress.Parse(expected));
    }

    [Fact]
    public async Task GetClientIp_ReturnsNull_IfEntryUnparseable()
    {
        var (sut, _) = await ProcessRequest("10.0.0.5, not-an-ip");

        sut.GetClientIp().Should().BeNull();
    }

    [Fact]
    public void GetClientIp_ReturnsNull_IfCaptureMiddlewareNotRun()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
        var sut = GetSut(context, CreateForwardedHeadersOptions(null));

        sut.GetClientIp().Should().BeNull();
    }

    [Fact]
    public async Task CaptureMiddleware_RemovesOriginVerifyHeader()
    {
        var (_, context) = await ProcessRequest("10.0.0.5, 203.0.113.7, 130.176.0.1", originVerify: Secret);

        context.Request.Headers.ContainsKey(OriginVerifyHeader).Should().BeFalse();
    }

    [Fact]
    public async Task CaptureMiddleware_RemovesOriginVerifyHeader_IfNoSecretsConfigured()
    {
        var (_, context) = await ProcessRequest("203.0.113.7", originVerify: "anything", secrets: []);

        context.Request.Headers.ContainsKey(OriginVerifyHeader).Should().BeFalse();
    }

    [Fact]
    public async Task GetClientIp_DoesNotChangeRemoteIpAddress()
    {
        // x-forwarded-for isn't processed by forwarded headers middleware, RemoteIpAddress remains the caller
        var (sut, context) = await ProcessRequest("10.0.0.5, 203.0.113.7, 130.176.0.1", originVerify: Secret);

        sut.GetClientIp().Should().Be(IPAddress.Parse("203.0.113.7"));
        context.Connection.RemoteIpAddress.Should().Be(LoadBalancerIp);
    }

    private static Task<(ClientIpResolver Sut, HttpContext Context)> ProcessRequest(string? forwardedFor,
        string? originVerify = null, List<string>? secrets = null, string? knownNetwork = null)
        => ProcessRequest(forwardedFor, originVerify == null ? [] : [originVerify], secrets, knownNetwork,
            LoadBalancerIp);

    private static Task<(ClientIpResolver Sut, HttpContext Context)> ProcessRequest(string? forwardedFor,
        string? knownNetwork, IPAddress? callerIp)
        => ProcessRequest(forwardedFor, [], null, knownNetwork, callerIp);

    /// <summary>
    /// Run request through capture and forwarded headers middleware, as configured in Startup
    /// </summary>
    private static async Task<(ClientIpResolver Sut, HttpContext Context)> ProcessRequest(string? forwardedFor,
        string[] originVerify, List<string>? secrets = null, string? knownNetwork = null,
        IPAddress? callerIp = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = callerIp;
        if (forwardedFor != null) context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        if (originVerify.Length > 0) context.Request.Headers[OriginVerifyHeader] = originVerify;

        var settings = new OrchestratorSettings();
        settings.Auth.CdnOriginVerify.Secrets = secrets ?? [Secret];
        var forwardedHeadersOptions = CreateForwardedHeadersOptions(knownNetwork);

        var forwardedHeaders = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance,
            Options.Create(forwardedHeadersOptions));
        var capture = new ClientIpCaptureMiddleware(forwardedHeaders.Invoke, Options.Create(settings));
        await capture.Invoke(context);

        return (GetSut(context, forwardedHeadersOptions), context);
    }

    private static ForwardedHeadersOptions CreateForwardedHeadersOptions(string? knownNetwork)
    {
        // As configured in Startup. No knownNetwork is "AllNetworks"
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
        };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        if (knownNetwork != null) options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(knownNetwork));
        return options;
    }

    private static ClientIpResolver GetSut(HttpContext context, ForwardedHeadersOptions options)
    {
        // Fake rather than HttpContextAccessor as that uses AsyncLocal, which doesn't flow out of async helper
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(context);
        return new ClientIpResolver(httpContextAccessor, Options.Create(options),
            NullLogger<ClientIpResolver>.Instance);
    }
}
