using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace DLCS.Repository.OriginStrategies;

/// <summary>
/// Default <see cref="IOriginAddressPolicy"/>, blocking a fixed set of ranges plus any that are configured.
/// </summary>
public class OriginAddressPolicy : IOriginAddressPolicy
{
    /// <summary>
    /// Ranges that are blocked regardless of configuration - loopback, link-local, unique-local and unspecified.
    /// Link-local and unique-local cover the cloud instance-metadata addresses (169.254.169.254, fd00:ec2::254);
    /// connecting to an unspecified address reaches loopback.
    /// </summary>
    private static readonly IPNetwork[] AlwaysBlocked =
    [
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("::1/128"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("fc00::/7"),
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

    public IPNetwork? GetBlockingRange(IPAddress address)
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
