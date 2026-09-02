using System.Net;

namespace DLCS.Repository.OriginStrategies;

/// <summary>
/// Determines whether we are permitted to connect to a given IP address when fetching from an origin.
/// </summary>
/// <remarks>
/// Origins are specified by API consumers so can be any host; without this an origin could be used to reach
/// services that are only meant to be reachable from inside the deployment.
/// </remarks>
public interface IOriginAddressPolicy
{
    /// <summary>
    /// Get the blocked range that specified address falls within, if any.
    /// </summary>
    /// <returns>Range that blocks this address, or null if the address can be connected to</returns>
    IPNetwork? GetBlockingRange(IPAddress address);
}
