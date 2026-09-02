using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using DLCS.Model.Customers;

namespace DLCS.Repository.OriginStrategies;

/// <summary>
/// Helpers for safely creating and validating the customer-supplied regex on a <see cref="CustomerOriginStrategy"/>.
/// </summary>
public static class OriginStrategyRegex
{
    /// <summary>
    /// Options always used when matching an origin against a CustomerOriginStrategy regex
    /// </summary>
    private const RegexOptions MatchOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>
    /// Create a <see cref="Regex"/> for given pattern, with ReDoS protections applied
    /// </summary>
    /// <param name="pattern">Regex pattern</param>
    /// <param name="settings">Settings controlling which protections are applied</param>
    /// <param name="nonBacktracking">
    /// On return, true if the regex was created with <see cref="RegexOptions.NonBacktracking"/>. False if
    /// NonBacktracking is disabled, or the pattern uses constructs it doesn't support and has fallen back to a
    /// backtracking match constrained by a timeout.
    /// </param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="pattern"/> is not a valid regex</exception>
    public static Regex Create(string pattern, OriginStrategySettings settings, out bool nonBacktracking)
    {
        if (settings.UseNonBacktracking)
        {
            try
            {
                var nonBacktrackingRegex =
                    new Regex(pattern, MatchOptions | RegexOptions.NonBacktracking, settings.MatchTimeout);
                nonBacktracking = true;
                return nonBacktrackingRegex;
            }
            catch (NotSupportedException)
            {
                // Pattern uses lookarounds, backreferences, atomic groups or conditionals - fall back below
            }
        }

        nonBacktracking = false;
        return new Regex(pattern, MatchOptions, settings.MatchTimeout);
    }

    /// <summary>
    /// Check whether given pattern is a valid regular expression
    /// </summary>
    /// <param name="pattern">Pattern to check</param>
    /// <param name="error">On return, the reason the pattern is invalid, else null</param>
    public static bool IsValidPattern(string pattern, [NotNullWhen(false)] out string? error)
    {
        try
        {
            _ = new Regex(pattern, MatchOptions);
            error = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Check whether given pattern can be matched using <see cref="RegexOptions.NonBacktracking"/>, which is the
    /// only way to guarantee that matching cannot be made to run in super-linear time.
    /// </summary>
    /// <remarks>Returns false for patterns that aren't valid regular expressions</remarks>
    public static bool SupportsNonBacktracking(string pattern)
    {
        try
        {
            _ = new Regex(pattern, MatchOptions | RegexOptions.NonBacktracking);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
