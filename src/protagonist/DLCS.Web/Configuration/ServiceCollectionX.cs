using DLCS.Core.Strings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using IPNetwork = System.Net.IPNetwork;

namespace DLCS.Web.Configuration;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>, related to web configuration
/// </summary>
public static class ServiceCollectionX
{
    /// <summary>
    /// Configures host to use x-forwarded-host and x-forwarded-proto to set httpContext.Request.Host and .Scheme
    /// respectively. Optionally uses x-forwarded-for to set httpContext.Connection.RemoteIpAddress.
    /// If "KnownNetworks" configuration key found, this will be used to set ForwardedHeadersOptions.KnownNetworks.
    /// If "ForwardLimit" configuration key found, this will be used to set ForwardedHeadersOptions.ForwardLimit
    /// </summary>
    /// <param name="services">Current <see cref="IServiceCollection"/></param>
    /// <param name="configuration">Current <see cref="IConfiguration"/></param>
    /// <param name="useForwardedFor">
    /// If true, x-forwarded-for is used to set RemoteIpAddress. Only required by services that use the client IP, e.g.
    /// to forward to IIIF Auth2 for granting IP roles
    /// </param>
    /// <remarks>
    /// If "KnownNetworks" key not found, all networks are allowed. This maintains the behaviour that was present in
    /// dotnet until .NET 8.0.17 + .NET 9.0.6 release and so avoids breaking changes.
    /// If "KnownNetworks" key is found then the default is maintained and any CIDR addresses are added.
    /// If <paramref name="useForwardedFor"/> is true, "KnownNetworks" should be set in deployed environments to
    /// prevent x-forwarded-for being spoofed by callers that can reach the service directly.
    /// </remarks>
    public static IServiceCollection ConfigureForwardedHeaders(this IServiceCollection services,
        IConfiguration configuration, bool useForwardedFor = false)
    {
        const string configurationKey = "KnownNetworks";
        const string allNetworks = "AllNetworks";
        var knownNetworks = configuration.GetValue(configurationKey, allNetworks)!;
        var forwardLimit = configuration.GetValue<int?>("ForwardLimit", 1);

        var logger = new SerilogLoggerFactory(Log.Logger).CreateLogger("ServiceCollection");
        
        // Use x-forwarded-host and x-forwarded-proto to set httpContext.Request.Host and .Scheme respectively, and
        // optionally x-forwarded-for to set .Connection.RemoteIpAddress
        return services.Configure<ForwardedHeadersOptions>(opts =>
        {
            opts.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
            if (useForwardedFor) opts.ForwardedHeaders |= ForwardedHeaders.XForwardedFor;
            opts.ForwardLimit = forwardLimit;

            if (knownNetworks.Equals(allNetworks))
            {
                logger.LogWarning(useForwardedFor
                    ? "Forwarded header values accepted from all networks and proxies. XFF Client IP can be spoofed"
                    : "Forwarded header values accepted from all networks and proxies");
                opts.KnownIPNetworks.Clear();
                opts.KnownProxies.Clear();
            }
            else
            {
                logger.LogInformation("Forwarded header values accepted from networks: {KnownNetworks}", knownNetworks);
                foreach (var kn in knownNetworks.SplitSeparatedString(","))
                {
                    opts.KnownIPNetworks.Add(IPNetwork.Parse(kn));
                }
            }
        });
    }
}
