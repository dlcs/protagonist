using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;

namespace DLCS.AWS.Configuration;

/// <summary>
/// Bounded, thread-safe cache of items keyed by customer id.
/// </summary>
/// <remarks>
/// This uses a dedicated cache, rather than the shared <see cref="IAppCache"/>, as the items held are long-lived
/// resources rather than cached data - evicting one means an additional STS session, so treat differently from general
/// application caching.
///
/// Items are not disposed when evicted; AWS clients and credentials hold no per-instance resources that need
/// releasing (HttpClients are cached and shared process-wide by the SDK) and disposing an item that still has
/// requests in flight would fail those requests.
/// </remarks>
internal sealed class CustomerKeyedCache<T> : IDisposable
    where T : class
{
    private readonly MemoryCache cache;
    private readonly IAppCache appCache;
    private readonly MemoryCacheEntryOptions entryOptions;

    public CustomerKeyedCache(int sizeLimit, TimeSpan idleTimeout)
    {
        cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit });
        appCache = new CachingService(new MemoryCacheProvider(cache));
        entryOptions = new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = idleTimeout };
    }

    /// <summary>
    /// Get item for specified customer, creating and caching it if not found.
    /// </summary>
    public T GetOrCreate(int customer, Func<int, T> factory)
        => appCache.GetOrAdd(customer.ToString(), () => factory(customer), entryOptions);

    public void Dispose() => cache.Dispose();
}
