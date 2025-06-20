using DLCS.Core.Strings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace DLCS.Web.Configuration;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>, related to web configuration
/// </summary>
public static class ServiceCollectionX
{
    /// <summary>
    /// Configures host to use x-forwarded-host and x-forwarded-proto to set httpContext.Request.Host and .Scheme
    /// respectively.
    /// If "KnownNetworks" configuration key found, this will be used to set ForwardedHeadersOptions.KnownNetworks 
    /// </summary>
    /// <remarks>
    /// If "KnownNetworks" key not found, all networks are allowed. This maintains the behaviour that was present in
    /// dotnet until .NET 8.0.17 + .NET 9.0.6 release and so avoids breaking changes.
    /// If "KnownNetworks" key is found then the default is maintained and any CIDR addresses are added
    /// </remarks>
    public static IServiceCollection ConfigureForwardedHeaders(this IServiceCollection services,
        IConfiguration configuration)
    {
        const string configurationKey = "KnownNetworks";
        const string allNetworks = "AllNetworks";
        var knownNetworks = configuration.GetValue(configurationKey, allNetworks)!;

        var logger = new SerilogLoggerFactory(Log.Logger).CreateLogger("ServiceCollection");
        
        // Use x-forwarded-host and x-forwarded-proto to set httpContext.Request.Host and .Scheme respectively
        return services.Configure<ForwardedHeadersOptions>(opts =>
        {
            opts.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;

            if (knownNetworks.Equals(allNetworks))
            {
                logger.LogWarning("Forwarded header values accepted from all networks and proxies");
                opts.KnownNetworks.Clear();
                opts.KnownProxies.Clear();
            }
            else
            {
                logger.LogInformation("Forwarded header values accepted from networks: {KnownNetworks}", knownNetworks);
                foreach (var kn in knownNetworks.SplitSeparatedString(","))
                {
                    opts.KnownNetworks.Add(IPNetwork.Parse(kn));
                }
            }
        });
    }
}
