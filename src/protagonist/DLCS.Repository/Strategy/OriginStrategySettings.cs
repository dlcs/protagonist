namespace DLCS.Repository.Strategy;

/// <summary>
/// Settings that control how items are fetched from an origin.
/// </summary>
public class OriginStrategySettings
{
    /// <summary>
    /// Name of appSettings section containing these settings.
    /// </summary>
    public const string SettingsSection = "OriginStrategy";

    /// <summary>
    /// Additional IP ranges, in CIDR notation, that an origin is forbidden from resolving to.
    /// Loopback and link-local ranges are always blocked, regardless of this value.
    /// </summary>
    public string[] BlockedIpRanges { get; set; } = [];
}
