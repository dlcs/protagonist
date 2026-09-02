using System;
using System.Text.RegularExpressions;
using DLCS.Model.Customers;
using Microsoft.Extensions.Configuration;

namespace DLCS.Repository.Customers;

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
    /// Maximum time a single origin/pattern match may take before being abandoned.
    /// </summary>
    public TimeSpan MatchTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

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
