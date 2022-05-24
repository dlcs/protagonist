using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace Orchestrator.Infrastructure.ReverseProxy
{
    /// <summary>
    /// This contains stuff from Yarp but helps take a ClusterConfig and generate a target from it
    /// </summary>
    public class DownstreamDestinationSelector
    {
        private readonly ILogger<DownstreamDestinationSelector> logger;
        private readonly IProxyStateLookup proxyStateLookup;
        private readonly IDictionary<string,ILoadBalancingPolicy> loadBalancingPolicies;

        public DownstreamDestinationSelector(
            ILogger<DownstreamDestinationSelector> logger, 
            IEnumerable<ILoadBalancingPolicy> loadBalancingPolicies,
            IProxyStateLookup proxyStateLookup)
        {
            this.loadBalancingPolicies = loadBalancingPolicies.ToDictionaryByUniqueId(p => p.Name);
            this.logger = logger;
            this.proxyStateLookup = proxyStateLookup;
        }
        
        public bool TryGetCluster(ProxyDestination destination, out ClusterState? clusterState)
        {
            clusterState = null;
            if (destination is ProxyDestination.S3 or ProxyDestination.Unknown)
            {
                return false;
            }

            var name = GetProxyNameForDestination(destination);
            var found = proxyStateLookup.TryGetCluster(name, out clusterState);
            return found;
        }
        
        public static string GetProxyNameForDestination(ProxyDestination destination) 
            => destination switch
            {
                ProxyDestination.Orchestrator => "deliverator",
                ProxyDestination.Thumbs => "thumbs",
                ProxyDestination.ResizeThumbs => "thumbresize",
                ProxyDestination.ImageServer =>"image_server",
                _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, null)
            };

        /// <summary>
        /// Use load-balancing to figure out where we want to redirect the current request.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="clusterState"></param>
        /// <returns></returns>
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
    }
}