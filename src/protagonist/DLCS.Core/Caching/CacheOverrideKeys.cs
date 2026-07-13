using System;
using System.Collections.Generic;

namespace DLCS.Core.Caching;

/// <summary>
/// Keys identifying caching actions that can have their ttl overridden via configuration.
/// </summary>
/// <remarks>
/// These values form a configuration contract - they are used as keys in the
/// "Caching:TimeToLive:{source}:Overrides" section, so renaming a value will stop any existing override from being
/// applied. Only actions listed here can be overridden; to make a new caching action overridable add a key here and
/// pass it to the relevant GetMemoryCacheOptions()/GetTtl() call.
///
/// Values must be valid shell identifiers (no "-" or "."), as config can be supplied via envvar - e.g.
/// "Caching__TimeToLive__Memory__Overrides__Policy=3600". Note that a single "_" is safe, only "__" is treated as a
/// section separator. Lookup is case-insensitive.
///
/// Note that these identify a caching *action*, they are not the key a cached item is stored under.
/// </remarks>
public static class CacheOverrideKeys
{
    /// <summary>
    /// Assets loaded from the database
    /// </summary>
    public const string Asset = "Asset";

    /// <summary>
    /// Adjuncts loaded from the database
    /// </summary>
    public const string Adjunct = "Adjunct";

    /// <summary>
    /// Assets tracked for orchestration
    /// </summary>
    public const string OrchestrationAsset = "OrchestrationAsset";

    /// <summary>
    /// Adjuncts tracked for orchestration
    /// </summary>
    public const string OrchestrationAdjunct = "OrchestrationAdjunct";

    /// <summary>
    /// Storage, thumbnail and image-optimisation policies
    /// </summary>
    public const string Policy = "Policy";

    /// <summary>
    /// Customer path element lookups (id/name resolution)
    /// </summary>
    public const string CustomerPath = "CustomerPath";

    /// <summary>
    /// All valid override keys.
    /// </summary>
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Asset, Adjunct, OrchestrationAsset, OrchestrationAdjunct, Policy, CustomerPath
    };
}
