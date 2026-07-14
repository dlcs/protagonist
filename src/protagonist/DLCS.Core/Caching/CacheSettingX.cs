using System;
using Microsoft.Extensions.Caching.Memory;

namespace DLCS.Core.Caching;

/// <summary>
/// A collection of extension methods to help working with CacheSettings
/// </summary>
public static class CacheSettingX
{
    /// <summary>
    /// Get <see cref="MemoryCacheEntryOptions"/> object with specified values.
    /// </summary>
    /// <param name="named">
    /// Optional <see cref="CacheOverrideKeys"/> value identifying this caching action. If a ttl override is configured for this
    /// key it takes precedence over <paramref name="duration"/>.
    /// </param>
    public static MemoryCacheEntryOptions GetMemoryCacheOptions(this CacheSettings cacheSettings,
        CacheDuration duration = CacheDuration.Default, long size = 1,
        CacheItemPriority priority = CacheItemPriority.Normal, string? named = null)
        => new()
        {
            Priority = priority,
            Size = size,
            AbsoluteExpirationRelativeToNow =
                TimeSpan.FromSeconds(cacheSettings.GetTtl(duration, CacheSource.Memory, named)),
        };
}