using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DLCS.Core.Collections;
using DLCS.Core.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace Orchestrator.Infrastructure.ReverseProxy;

/// <summary>
/// Wrapper around YARP functionality to help manage and access downstream destinations
/// </summary>
/// <remarks>This contains some code internalised from YARP repo</remarks>
public class DownstreamDestinationSelector
{
    private readonly ILogger<DownstreamDestinationSelector> logger;
    private readonly IProxyStateLookup proxyStateLookup;
    private readonly IOptionsMonitor<OrchestratorSettings> orchestratorSettings;
    private readonly IDictionary<string, ILoadBalancingPolicy> loadBalancingPolicies;

    public DownstreamDestinationSelector(
        ILogger<DownstreamDestinationSelector> logger, 
        IEnumerable<ILoadBalancingPolicy> loadBalancingPolicies,
        IProxyStateLookup proxyStateLookup,
        IOptionsMonitor<OrchestratorSettings> orchestratorSettings)
    {
        this.loadBalancingPolicies = loadBalancingPolicies.ToDictionaryByUniqueId(p => p.Name);
        this.logger = logger;
        this.proxyStateLookup = proxyStateLookup;
        this.orchestratorSettings = orchestratorSettings;
    }

    /// <summary>
    /// Attempt to get <see cref="ClusterState"/> object for specified <see cref="ProxyDestination"/>
    /// </summary>
    /// <param name="destination">Destination to get ClusterState for</param>
    /// <param name="clusterState">ClusterState, if found</param>
    /// <returns>true if ClusterState found, else false</returns>
    public bool TryGetCluster(ProxyDestination destination, [NotNullWhen(true)] out ClusterState? clusterState)
    {
        clusterState = null;
        if (destination is ProxyDestination.S3 or ProxyDestination.Unknown)
        {
            logger.LogWarning("Attempt to get a cluster for {ClusterDestination} that will never have destinations",
                destination);
            return false;
        }

        var name = GetProxyNameForDestination(destination);
        var found = proxyStateLookup.TryGetCluster(name, out clusterState);
        return found;
    }

    /// <summary>
    /// Use load-balancing to figure out where we want to redirect the current request.
    /// </summary>
    /// <remarks>
    /// This is heavily influenced by Yarp LoadBalancingMiddleware, but without dependency on
    /// IReverseProxyFeature as we're using Direct Forwarding
    /// </remarks>
    public DestinationState? GetClusterTarget(HttpContext context, ClusterState clusterState)
    {
        var destinations = clusterState.DestinationsState.AvailableDestinations;

        var destinationCount = destinations.Count;
        DestinationState? destination;

        if (destinationCount == 0)
        {
            destination = null;
        }
        else if (destinationCount == 1)
        {
            destination = destinations[0];
        }
        else
        {
            var currentPolicy = loadBalancingPolicies.GetRequiredServiceById(clusterState.Model.Config.LoadBalancingPolicy,
                LoadBalancingPolicies.PowerOfTwoChoices);
            destination = currentPolicy.PickDestination(context, clusterState, destinations);
        }

        if (destination == null)
        {
            logger.LogWarning("Unable to find health destination for {ClusterId}", clusterState.ClusterId);
        }

        return destination;
    }

    /// <summary>
    /// Get a random, healthy address for specified <see cref="ProxyDestination"/>.
    /// This is more light weight than GetClusterTarget() as it doesn't use load-balancing, it picks a random
    /// target from those available.
    /// </summary>
    public string? GetRandomDestinationAddress(ProxyDestination destination)
    {
        if (!TryGetCluster(destination, out var clusterState))
        {
            logger.LogWarning("Attempt to get a random address for {ProxyDestination} but could not get cluster",
                destination);
            return null;
        }
        
        var destinations = clusterState!.DestinationsState.AvailableDestinations;
        var destinationCount = destinations.Count;

        if (destinationCount == 0) return null;
        
        var destinationState = destinationCount == 1 ? destinations[0] : destinations.PickRandom();
        return destinationState.Model.Config.Address;
    }

    private string GetProxyNameForDestination(ProxyDestination destination)
        => destination switch
        {
            ProxyDestination.Orchestrator => "deliverator",
            ProxyDestination.Thumbs => "thumbs",
            ProxyDestination.ResizeThumbs => "thumbresize",
            ProxyDestination.SpecialServer => "specialserver",
            ProxyDestination.ImageServer => GetImageServerCluster(),
            _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, null)
        };

    private string GetImageServerCluster()
        => orchestratorSettings.CurrentValue.ImageServer switch
        {
            ImageServer.IIPImage => "iip",
            ImageServer.Cantaloupe => "cantaloupe",
            _ => throw new ArgumentOutOfRangeException()
        };
}

// Copied from https://github.com/microsoft/reverse-proxy/blob/9bbdf5eec851dbd5b5c4a473effa6cc8b2197da4/src/ReverseProxy/Utilities/ServiceLookupHelper.cs
internal static class ProxyStateLookupUtils
{
    public static IDictionary<string, T> ToDictionaryByUniqueId<T>(this IEnumerable<T> services, Func<T, string> idSelector)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

        foreach (var service in services)
        {
            if (!result.TryAdd(idSelector(service), service))
            {
                throw new ArgumentException($"More than one {typeof(T)} found with the same identifier.", nameof(services));
            }
        }

        return result;
    }
    
    public static T GetRequiredServiceById<T>(this IDictionary<string, T> services, string? id, string defaultId)
    {
        var lookup = id;
        if (string.IsNullOrEmpty(lookup))
        {
            lookup = defaultId;
        }

        if (!services.TryGetValue(lookup, out var result))
        {
            throw new ArgumentException($"No {typeof(T)} was found for the id '{lookup}'.", nameof(id));
        }
        return result;
    }
}