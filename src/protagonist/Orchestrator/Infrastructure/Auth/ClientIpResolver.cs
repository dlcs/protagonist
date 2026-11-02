using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.Auth;

/// <summary>
/// Provides the IP address of the client making the current request, for forwarding to downstream services.
/// </summary>
/// <remarks>
/// Currently only used for IIIF Auth2 downstream requests to evaluate IP restrictions. Rather than forward all IPs to
/// downstream callers, Orchestrator works out the correct value to pass, keeping that logic in a single service
/// </remarks>
public interface IClientIpResolver
{
    /// <summary>
    /// Get the client IP address for current request, or null if it can't be determined.
    /// IPv4-mapped IPv6 addresses are returned as IPv4.
    /// </summary>
    IPAddress? GetClientIp();
}

/// <summary>
/// Request values captured by <see cref="ClientIpCaptureMiddleware"/>, before forwarded headers middleware has
/// processed them
/// </summary>
/// <param name="ForwardedFor">x-forwarded-for header value as received, may be empty</param>
/// <param name="CallerIp">IP address of the direct caller (e.g. load balancer), may be null</param>
/// <param name="ViaCdn">Whether request had a valid CDN origin verify header</param>
public record ReceivedClientRequest(string ForwardedFor, IPAddress? CallerIp, bool ViaCdn);

/// <summary>
/// <see cref="IClientIpResolver"/> that determines client IP from x-forwarded-for, as received by Orchestrator.
/// </summary>
/// <remarks>
/// Orchestrator may be accessed via a CDN (e.g. CloudFront, Cloudflare) and load balancer, or directly via load
/// balancer, so the client's position in x-forwarded-for varies:
/// - via CDN: "[spoof,] viewer, cdn" - client is 2nd from right, added by CDN
/// - direct: "[spoof,] client" - client is rightmost, added by load balancer
/// Requests that came via the CDN are identified by a secret custom header added by the CDN, see
/// <see cref="CdnOriginVerifySettings"/>. Without a valid secret the rightmost entry is used, so a direct caller
/// cannot spoof their IP.
/// Forwarded headers middleware doesn't process x-forwarded-for, so <see cref="ConnectionInfo.RemoteIpAddress"/>
/// remains the direct caller (e.g. load balancer); this resolver doesn't alter it.
/// Requires <see cref="ClientIpCaptureMiddleware"/> to run prior to forwarded headers middleware.
/// </remarks>
public class ClientIpResolver(
    IHttpContextAccessor httpContextAccessor,
    IOptions<ForwardedHeadersOptions> forwardedHeadersOptions,
    ILogger<ClientIpResolver> logger) : IClientIpResolver
{
    internal const string ReceivedRequestKey = "__ReceivedClientRequest";

    public IPAddress? GetClientIp()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Items[ReceivedRequestKey] is not ReceivedClientRequest received)
        {
            logger.LogDebug("No received request values captured, unable to determine client IP");
            return null;
        }

        if (!IsTrustedCaller(received.CallerIp))
        {
            logger.LogTrace("Caller {CallerIp} is not a known proxy, unable to determine client IP",
                received.CallerIp);
            return null;
        }

        var entries = received.ForwardedFor.Split(',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        // CDN adds viewer IP, load balancer then adds CDN IP
        var positionFromRight = received.ViaCdn ? 2 : 1;
        if (entries.Length < positionFromRight)
        {
            logger.LogDebug(
                "x-forwarded-for has {EntryCount} entries, position {Position} required (via CDN: {ViaCdn})",
                entries.Length, positionFromRight, received.ViaCdn);
            return null;
        }

        if (!IPEndPoint.TryParse(entries[^positionFromRight], out var endPoint))
        {
            logger.LogDebug("Unable to parse x-forwarded-for entry at position {Position}", positionFromRight);
            return null;
        }

        var clientIp = endPoint.Address.IsIPv4MappedToIPv6 ? endPoint.Address.MapToIPv4() : endPoint.Address;
        logger.LogDebug("Resolved client IP {ClientIp} from x-forwarded-for position {Position} (via CDN: {ViaCdn})",
            clientIp, positionFromRight, received.ViaCdn);
        return clientIp;
    }

    private bool IsTrustedCaller(IPAddress? callerIp)
    {
        // Matches forwarded headers middleware: a null remote IP is allowed for servers that don't support it (e.g.
        // TestServer), and if no known proxies or networks are configured ("AllNetworks") all callers are trusted
        if (callerIp == null) return true;

        var options = forwardedHeadersOptions.Value;
        if (options.KnownProxies.Count == 0 && options.KnownIPNetworks.Count == 0) return true;

        // Known proxies/networks may be configured as IPv4 or IPv4-mapped IPv6, so check both forms
        var ipv4 = callerIp.IsIPv4MappedToIPv6 ? callerIp.MapToIPv4() : callerIp;
        var mapped = ipv4.AddressFamily == AddressFamily.InterNetwork ? ipv4.MapToIPv6() : ipv4;
        return options.KnownProxies.Any(p => p.Equals(ipv4) || p.Equals(mapped)) ||
               options.KnownIPNetworks.Any(n => n.Contains(ipv4) || n.Contains(mapped));
    }
}

/// <summary>
/// Middleware that captures request values required by <see cref="ClientIpResolver"/>, before forwarded headers
/// middleware has processed them. Validates and then removes the CDN origin verify header, so it's never logged
/// or proxied downstream.
/// </summary>
/// <remarks>
/// The CDN should replace a client-supplied header with the same name as the one it adds (to be confirmed for
/// CloudFront custom origin headers). Either way a client, or a direct caller, can't use the header without knowing
/// the secret.
/// </remarks>
public class ClientIpCaptureMiddleware(RequestDelegate next, IOptions<OrchestratorSettings> orchestratorOptions)
{
    public Task Invoke(HttpContext context)
    {
        var settings = orchestratorOptions.Value.Auth.CdnOriginVerify;
        var request = context.Request;

        var viaCdn = IsValidOriginVerify(request.Headers[settings.HeaderName], settings);

        // Remove secret so it's never logged or proxied downstream (proxied requests copy request headers)
        request.Headers.Remove(settings.HeaderName);

        context.Items[ClientIpResolver.ReceivedRequestKey] = new ReceivedClientRequest(
            request.Headers[ForwardedHeadersDefaults.XForwardedForHeaderName].ToString(),
            context.Connection.RemoteIpAddress,
            viaCdn);

        return next(context);
    }

    private static bool IsValidOriginVerify(StringValues headerValues, CdnOriginVerifySettings settings)
    {
        if (!settings.IsEnabled || headerValues.Count != 1 || string.IsNullOrEmpty(headerValues[0])) return false;

        // Header and configured secrets are both plain values. Hash both before comparing so FixedTimeEquals gets
        // equal-length inputs and neither the value nor its length leaks via timing. All secrets are checked
        var receivedHash = SHA256.HashData(Encoding.UTF8.GetBytes(headerValues[0]!));
        var isValid = false;
        foreach (var secret in settings.Secrets.Where(s => !string.IsNullOrEmpty(s)))
        {
            isValid |= CryptographicOperations.FixedTimeEquals(receivedHash,
                SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        }

        return isValid;
    }
}

public static class ClientIpCaptureMiddlewareX
{
    /// <summary>
    /// Add <see cref="ClientIpCaptureMiddleware"/> to application builder. Must be added before forwarded headers
    /// middleware
    /// </summary>
    public static IApplicationBuilder UseClientIpCapture(this IApplicationBuilder builder)
        => builder.UseMiddleware<ClientIpCaptureMiddleware>();
}
