using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace DLCS.Repository.Strategy.Network;

/// <summary>
/// Determines whether we are permitted to connect to a given IP address when fetching from an origin.
/// </summary>
/// <remarks>
/// Origins are specified by API consumers so can be any host; without this an origin could be used to reach
/// services that are only meant to be reachable from inside the deployment.
/// </remarks>
public class OriginAddressPolicy
{
    /// <summary>
    /// Ranges that are blocked regardless of configuration - loopback, link-local and unspecified. Link-local covers
    /// the cloud instance-metadata address (169.254.169.254); connecting to an unspecified address reaches loopback.
    /// </summary>
    private static readonly IPNetwork[] AlwaysBlocked =
    [
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("::1/128"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("::/128")
    ];

    private readonly IPNetwork[] blockedRanges;

    /// <param name="additionalBlockedRanges">
    /// Further ranges to block, in CIDR notation. Throws <see cref="FormatException"/> if any are invalid.
    /// </param>
    public OriginAddressPolicy(IEnumerable<string>? additionalBlockedRanges = null)
    {
        blockedRanges = AlwaysBlocked.Concat(ParseRanges(additionalBlockedRanges)).ToArray();
    }

    /// <summary>
    /// Get the blocked range that specified address falls within, if any.
    /// </summary>
    /// <returns>Range that blocks this address, or null if the address can be connected to</returns>
    public virtual IPNetwork? GetBlockingRange(IPAddress address)
    {
        // An IPv4 address can be expressed in IPv6 form (::ffff:127.0.0.1) so check both representations
        var mapped = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : null;

        foreach (var range in blockedRanges)
        {
            if (range.Contains(address)) return range;
            if (mapped != null && range.Contains(mapped)) return range;
        }

        return null;
    }

    private static IEnumerable<IPNetwork> ParseRanges(IEnumerable<string>? ranges)
        => (ranges ?? []).Select(range => IPNetwork.TryParse(range, out var parsed)
            ? parsed
            : throw new FormatException($"'{range}' is not a valid CIDR IP range"));
}
