using System;
using System.Text.RegularExpressions;
using DLCS.Model.Customers;
using Microsoft.Extensions.Configuration;

namespace DLCS.Repository.OriginStrategies;

/// <summary>
/// Controls how the customer-supplied regex on a <see cref="CustomerOriginStrategy"/> is evaluated, guarding
/// against catastrophic backtracking (ReDoS).
/// </summary>
public class OriginStrategyRegexSettings
{
    public const string ConfigSection = "OriginStrategyRegex";

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
    public TimeSpan MatchTimeout { get; set; } = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Bind settings from configuration, falling back to defaults if the section is absent.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if configured values are not usable</exception>
    public static OriginStrategyRegexSettings FromConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(ConfigSection).Get<OriginStrategyRegexSettings>() ?? new();

        if (settings.MatchTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"appsetting:{ConfigSection}:{nameof(MatchTimeout)} must be greater than zero",
                nameof(configuration));
        }

        return settings;
    }
}
