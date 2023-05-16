using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orchestrator.Infrastructure.ReverseProxy;

namespace Orchestrator.Infrastructure;

/// <summary>
/// <see cref="IHealthCheck"/> that uses Yarp cluster config to check downstream status
/// </summary>
public class ProxyHealthCheck : IHealthCheck
{
    private readonly ProxyDestination proxyDestination;
    private readonly DownstreamDestinationSelector destinationSelector;

    public ProxyHealthCheck(ProxyDestination proxyDestination, DownstreamDestinationSelector destinationSelector)
    {
        this.proxyDestination = proxyDestination;
        this.destinationSelector = destinationSelector;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = new())
        => Task.FromResult(GetCheckResult());

    private HealthCheckResult GetCheckResult()
    {
        if (!destinationSelector.TryGetCluster(proxyDestination, out var clusterState))
        {
            return HealthCheckResult.Unhealthy("Cluster not found", data: GetData());
        }

        var registered = clusterState!.DestinationsState.AllDestinations.Count;
        var available = clusterState.DestinationsState.AvailableDestinations.Count;

        if (registered == 0)
        {
            return HealthCheckResult.Unhealthy("No cluster destinations found", data: GetData(0, 0));
        }

        if (available == 0)
        {
            return HealthCheckResult.Unhealthy("No available cluster destinations", data: GetData(registered, 0));
        }

        return HealthCheckResult.Healthy("Healthy", data: GetData(registered, available));
    }

    private Dictionary<string, object> GetData(int? registered = null, int? available = null)
    {
        var dataDictionary = new Dictionary<string, object> { ["Destination"] = proxyDestination.ToString() };
        if (registered.HasValue) dataDictionary["Registered"] = registered.Value;
        if (available.HasValue) dataDictionary["Available"] = available.Value;
        return dataDictionary;
    }
}

public static class HealthCheckX
{
    /// <summary>
    /// Add a health check for Yarp <see cref="ProxyDestination"/>
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="proxyDestination">The destination check is for</param>
    /// <param name="name">The health check name. Optional. If <c>null</c> the type proxyDestination.ToString() will be used for the name.</param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> that should be reported when the health check fails. Optional. If <c>null</c> then
    /// the default status of <see cref="HealthStatus.Unhealthy"/> will be reported.
    /// </param>
    /// <param name="tags">A list of tags that can be used to filter sets of health checks. Optional.</param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> representing the timeout of the check.</param>
    /// <returns>The specified <paramref name="builder"/>.</returns>
    public static IHealthChecksBuilder AddProxyDestination(this IHealthChecksBuilder builder,
        ProxyDestination proxyDestination,
        string? name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default,
        TimeSpan? timeout = default)
        => builder.Add(new HealthCheckRegistration(
            name ?? proxyDestination.ToString(),
            sp => ActivatorUtilities.CreateInstance<ProxyHealthCheck>(sp, proxyDestination),
            failureStatus,
            tags,
            timeout));
}