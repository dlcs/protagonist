using System;
using Microsoft.Extensions.Caching.Memory;

namespace DLCS.Repository.Caching
{
    /// <summary>
    /// A collection of extension methods to help working with CacheSettings
    /// </summary>
    public static class CacheSettingX
    {
        /// <summary>
        /// Get <see cref="MemoryCacheEntryOptions"/> object with specified values.
        /// </summary>
        public static MemoryCacheEntryOptions GetMemoryCacheOptions(this CacheSettings cacheSettings,
            CacheDuration duration = CacheDuration.Default, long size = 1,
            CacheItemPriority priority = CacheItemPriority.Normal)
            => new()
            {
                Priority = priority,
                Size = size,
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromSeconds(cacheSettings.GetTtl(duration, CacheSource.Memory)),
            };
    }
}