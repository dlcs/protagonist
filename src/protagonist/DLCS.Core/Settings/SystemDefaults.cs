using System;

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

    /// <summary>
    /// The system default minimum allowed value for the maxWidth property.
    /// </summary>
    public const int MinimumMaxWidth = 256;

    /// <summary>
    /// The default Postgres version used to configure EF's Npgsql provider and integration test containers.
    /// </summary>
    public static readonly Version PostgresVersion = new(18, 0);
}
