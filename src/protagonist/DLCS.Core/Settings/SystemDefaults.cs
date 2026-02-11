namespace DLCS.Core.Settings;

public static class SystemDefaults
{
    /// <summary>
    /// The system default max_width property.
    /// </summary>
    /// <remarks>
    /// This is used by API to prevent saving images that exceed this and Orchestrator to prevent serving image requests
    /// that exceed this.
    /// </remarks>
    public const int MaxWidth = 5000;
}
