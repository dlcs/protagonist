using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace DLCS.Repository.OriginStrategies;

/// <summary>
/// Default <see cref="IOriginAddressPolicy"/>, blocking a fixed set of ranges plus any that are configured.
/// </summary>
/// <remarks>
/// Precedence is deliberately flat: an address in any allowed range is permitted, whether it was blocked by
/// configuration or by <see cref="DefaultBlocked"/>. The exception is <see cref="NeverAllowable"/>, which no
/// configuration can open up.
/// </remarks>
public class OriginAddressPolicy : IOriginAddressPolicy
{
    /// <summary>
    /// Ranges that are default blocked unless explicitly allowed - loopback, link-local, unique-local and unspecified.
    /// Link-local and unique-local cover the cloud instance-metadata addresses; connecting to an unspecified
    /// address reaches loopback.
    /// </summary>
    private static readonly IPNetwork[] DefaultBlocked =
    [
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("::1/128"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("fc00::/7"),
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("::/128")
    ];

    /// <summary>
    /// Cloud instance-metadata addresses. No origin has any business reaching these, so they stay blocked even if
    /// covered by <see cref="OriginStrategySettings.AllowedIpRanges"/>.
    /// </summary>
    private static readonly IPNetwork[] NeverAllowable =
    [
        IPNetwork.Parse("169.254.169.254/32"),
        IPNetwork.Parse("fd00:ec2::254/128")
    ];

    private readonly IPNetwork[] allowedRanges;
    private readonly IPNetwork[] blockedRanges;

    /// <exception cref="FormatException">Thrown if any configured range is not valid CIDR notation</exception>
    public OriginAddressPolicy(OriginStrategySettings settings)
    {
        allowedRanges = ParseRanges(settings.AllowedIpRanges).ToArray();
        blockedRanges = DefaultBlocked.Concat(ParseRanges(settings.BlockedIpRanges)).ToArray();
    }

    /// <summary>
    /// Ranges an origin is permitted to resolve to despite being otherwise blocked. Empty unless configured.
    /// </summary>
    public IReadOnlyList<IPNetwork> AllowedRanges => allowedRanges;

    /// <summary>
    /// Attempt to get a blocking <see cref="IPNetwork"/> - returns null if there are no blocking ranges
    /// </summary>
    public IPNetwork? GetBlockingRange(IPAddress address)
    {
        if (FindRange(NeverAllowable, address) is { } metadataAddress) return metadataAddress;
        if (FindRange(allowedRanges, address) != null) return null;

        return FindRange(blockedRanges, address);
    }

    /// <summary>
    /// Find the first range containing specified address. An IPv4 address can be expressed in IPv6 form
    /// (::ffff:127.0.0.1) so both representations are checked - allowed and blocked ranges alike, otherwise the
    /// mapped form of an address would match one list but not the other.
    /// </summary>
    private static IPNetwork? FindRange(IPNetwork[] ranges, IPAddress address)
    {
        var mapped = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : null;

        foreach (var range in ranges)
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
