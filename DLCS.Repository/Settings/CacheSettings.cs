using System.Collections.Generic;

namespace DLCS.Repository.Settings
{
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
        /// Get pre configured Ttl for a source.
        /// Falls back to Memory cache duration if not found.
        /// </summary>
        /// <param name="duration">Pre configured ttl to fetch</param>
        /// <param name="source">Cache source to get ttl for</param>
        /// <returns>Ttl, in secs</returns>
        public int GetTtl(CacheDuration duration = CacheDuration.Default, CacheSource source = CacheSource.Memory)
            => TimeToLive.TryGetValue(source, out var settings)
                ? settings.GetTtl(duration)
                : GetFallback(duration);
        
        /// <summary>
        /// Get specific named cache override for source.
        /// Falls back to default memory duration if not found.
        /// </summary>
        /// <param name="named">Name of cache override to fetch</param>
        /// <param name="source">Cache source to get ttl for</param>
        /// <returns>Ttl, in secs</returns>
        public int GetTtl(string named, CacheSource source = CacheSource.Memory)
            => TimeToLive.TryGetValue(source, out var settings)
                ? settings.GetTtl(named)
                : GetFallback();

        private readonly CacheGroupSettings fallback = new();

        private int GetFallback(CacheDuration duration = CacheDuration.Default) =>
            TimeToLive.TryGetValue(CacheSource.Memory, out var settings)
                ? settings.GetTtl(duration)
                : fallback.GetTtl(duration);
    }

    public class CacheGroupSettings
    {
        public int ShortTtlSecs { get; set; } = 60;
        public int DefaultTtlSecs { get; set; } = 600;
        public int LongTtlSecs { get; set; } = 1800;
        public Dictionary<string, int> Overrides { get; set; }

        public int GetTtl(CacheDuration duration)
            => duration switch
            {
                CacheDuration.Short => ShortTtlSecs,
                CacheDuration.Default => DefaultTtlSecs,
                CacheDuration.Long => LongTtlSecs,
                _ => DefaultTtlSecs
            };

        public int GetTtl(string named)
            => Overrides.TryGetValue(named, out var ttl) ? ttl : DefaultTtlSecs;
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
}