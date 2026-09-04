using System;
using System.Net;
using System.Text.RegularExpressions;
using DLCS.Model.Customers;
using Microsoft.Extensions.Configuration;

namespace DLCS.Repository.OriginStrategies;

/// <summary>
/// Controls how the customer-supplied regex on a <see cref="CustomerOriginStrategy"/> is evaluated, guarding
/// against catastrophic backtracking (ReDoS), and which addresses an origin may be fetched from.
/// </summary>
public class OriginStrategySettings
{
    public const string ConfigSection = "OriginStrategy";

    /// <summary>
    /// If true, origins are matched using <see cref="RegexOptions.NonBacktracking"/>, which guarantees matching
    /// completes in time linear to the length of the origin regardless of how the pattern is written.
    /// Patterns using constructs NonBacktracking doesn't support (lookarounds, backreferences, atomic groups)
    /// fall back to a standard backtracking match constrained by <see cref="MatchTimeout"/>.
    /// </summary>
    public bool UseNonBacktracking { get; set; } = true;

    /// <summary>
    /// If true the API rejects patterns that can't be evaluated with <see cref="RegexOptions.NonBacktracking"/>,
    /// so every stored pattern is guaranteed to match in linear time. Disable to allow lookarounds, backreferences
    /// etc, which then rely on <see cref="MatchTimeout"/> alone.
    /// </summary>
    /// <remarks>
    /// Independent of <see cref="UseNonBacktracking"/> - this only governs what the API accepts, not how matching
    /// is performed.
    /// </remarks>
    public bool RejectBacktrackingPatterns { get; set; } = true;

    /// <summary>
    /// Maximum time a single origin/pattern match may take before being abandoned. Matching an origin legitimately
    /// takes microseconds, so this is deliberately tight - it is the only protection for patterns that fall back to
    /// backtracking.
    /// </summary>
    public TimeSpan MatchTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Additional IP ranges, in CIDR notation, that an origin is forbidden from resolving to.
    /// Loopback, link-local, unique-local and unspecified ranges are default blocked, regardless of this value.
    /// </summary>
    public string[] BlockedIpRanges { get; set; } = [];

    /// <summary>
    /// IP ranges, in CIDR notation, that an origin is permitted to resolve to even if otherwise blocked. Intended
    /// for local development, and deployments whose origins legitimately sit on internal addresses.
    /// </summary>
    /// <remarks>
    /// An allowed range wins over <see cref="BlockedIpRanges"/> and over the always-blocked ranges, so setting this
    /// widely (eg "0.0.0.0/0") disables the protection. The cloud instance-metadata addresses can never be allowed.
    /// </remarks>
    public string[] AllowedIpRanges { get; set; } = [];

    /// <summary>
    /// Bind settings from configuration, falling back to defaults if the section is absent.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if configured values are not usable</exception>
    public static OriginStrategySettings FromConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(ConfigSection).Get<OriginStrategySettings>() ?? new();

        if (settings.MatchTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"appsetting:{ConfigSection}:{nameof(MatchTimeout)} must be greater than zero",
                nameof(configuration));
        }

        EnsureValidRanges(settings.BlockedIpRanges, nameof(BlockedIpRanges));
        EnsureValidRanges(settings.AllowedIpRanges, nameof(AllowedIpRanges));

        return settings;

        void EnsureValidRanges(string[] ranges, string settingName)
        {
            foreach (var range in ranges)
            {
                if (!IPNetwork.TryParse(range, out _))
                {
                    throw new ArgumentException(
                        $"appsetting:{ConfigSection}:{settingName} contains '{range}', which is not a valid CIDR IP range",
                        nameof(configuration));
                }
            }
        }
    }
}
