using System;
using System.Collections.Generic;
using System.Linq;
using Orchestrator.Infrastructure.ReverseProxy;

namespace Orchestrator.Settings
{
    /// <summary>
    /// ReverseProxy settings
    /// </summary>
    /// <remarks>
    /// This is used by YARP but we need access to them avoid having duplicate config setup for declaring
    /// cluster destinations. Ideally the need for this will be removed with future releases of YARP.
    /// </remarks>
    public class ReverseProxySettings
    {
        /// <summary>
        /// Get a list of declared Clusters
        /// </summary>
        public Dictionary<string, ClusterConfig> Clusters { get; set; }
        
        /// <summary>
        /// Get destination address for specified ProxyDestination
        /// </summary>
        /// <remarks>Assumes 1 address per destination and has no error handling</remarks>
        public Uri? GetAddressForProxyTarget(ProxyDestination destination) 
            => destination switch
            {
                ProxyDestination.Orchestrator => GetAddressForCluster("deliverator"),
                ProxyDestination.Thumbs => GetAddressForCluster("thumbs"),
                ProxyDestination.ResizeThumbs => GetAddressForCluster("thumbresize"),
                ProxyDestination.ImageServer => GetAddressForCluster("image_server"),
                ProxyDestination.S3 => null,
                ProxyDestination.Unknown => null,
                _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, null)
            };

        private Uri GetAddressForCluster(string key)
            => Clusters[key].Destinations.First().Value.Address;
    }
    
    /// <summary>
    /// Represents an individual Cluster
    /// </summary>
    public class ClusterConfig
    {
        /// <summary>
        /// Get a list of destinations for cluster
        /// </summary>
        public Dictionary<string, DestinationConfig> Destinations { get; set; }
    }

    /// <summary>
    /// Represents an individual destination within a cluster
    /// </summary>
    public class DestinationConfig
    {
        /// <summary>
        /// Get the address associated with destination
        /// </summary>
        public Uri Address { get; set; }
    }
}