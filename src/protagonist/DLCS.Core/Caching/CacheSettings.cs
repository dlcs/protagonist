using System;
using System.Collections.Generic;

namespace DLCS.Core.Caching;

/// <summary>
/// Settings related to caching
/// </summary>
public class CacheSettings
{
    /// <summary>
    /// A collection CacheTtls per source
    /// </summary>
    public Dictionary<CacheSource, CacheGroupSettings> TimeToLive { get; set; } = new();

    /// <summary>
    /// The size limit for MemoryCache. Maps to MemoryCacheOptions.SizeLimit property 
    /// </summary>
    public long MemoryCacheSizeLimit { get; set; } = 10000;

    /// <summary>
    /// The amount to compact the cache by when the maximum size is exceeded, value be between 0 and 1.
    /// Maps to MemoryCacheOptions.CompactionPercentage property. 
    /// </summary>
    public double MemoryCacheCompactionPercentage { get; set; } = 0.05;

    /// <summary>
    /// Get pre configured Ttl for a source.
    /// If a named override is configured for the source that value is used, else the value for specified duration.
    /// Falls back to Memory cache duration if source not found.
    /// </summary>
    /// <param name="duration">Pre configured ttl to fetch, used if no override configured for <paramref name="named"/></param>
    /// <param name="source">Cache source to get ttl for</param>
    /// <param name="named">Optional name of cache override to fetch, see <see cref="CacheOverrideKeys"/></param>
    /// <returns>Ttl, in secs</returns>
    public int GetTtl(CacheDuration duration = CacheDuration.Default, CacheSource source = CacheSource.Memory,
        string? named = null)
        => TimeToLive.TryGetValue(source, out var settings)
            ? settings.GetTtl(duration, named)
            : GetFallback(duration, named);

    private readonly CacheGroupSettings fallback = new();

    private int GetFallback(CacheDuration duration, string? named) =>
        TimeToLive.TryGetValue(CacheSource.Memory, out var settings)
            ? settings.GetTtl(duration, named)
            : fallback.GetTtl(duration, named);
}

public class CacheGroupSettings
{
    public int ShortTtlSecs { get; set; } = 60;
    public int DefaultTtlSecs { get; set; } = 600;
    public int LongTtlSecs { get; set; } = 1800;

    /// <summary>
    /// Ttl overrides, in secs, for specific caching actions. Keyed by <see cref="CacheOverrideKeys"/> value.
    /// Case-insensitive, as envvar-supplied keys can't be relied on to match the casing of the constant.
    /// </summary>
    public Dictionary<string, int> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int GetTtl(CacheDuration duration, string? named = null)
        => named != null && Overrides.TryGetValue(named, out var ttl)
            ? ttl
            : GetDurationTtl(duration);

    private int GetDurationTtl(CacheDuration duration)
        => duration switch
        {
            CacheDuration.Short => ShortTtlSecs,
            CacheDuration.Default => DefaultTtlSecs,
            CacheDuration.Long => LongTtlSecs,
            _ => DefaultTtlSecs
        };
}

/// <summary>
/// Available caching sources
/// </summary>
public enum CacheSource
{
    /// <summary>
    /// Local in-memory cache
    /// </summary>
    Memory,
    
    /// <summary>
    /// External distributed cache
    /// </summary>
    Distributed,
    
    /// <summary>
    /// Http caching (via headers)
    /// </summary>
    Http
}

/// <summary>
/// Default preconfigured cache durations
/// </summary>
public enum CacheDuration
{
    Short,
    Default,
    Long
}