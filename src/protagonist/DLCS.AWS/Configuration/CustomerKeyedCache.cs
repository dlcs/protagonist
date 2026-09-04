using Microsoft.Extensions.Caching.Memory;

namespace DLCS.AWS.Configuration;

/// <summary>
/// Bounded, thread-safe cache of items keyed by customer id.
/// </summary>
/// <remarks>
/// Items are not disposed when evicted; AWS clients and credentials hold no per-instance resources that need
/// releasing (HttpClients are cached and shared process-wide by the SDK) and disposing an item that still has
/// requests in flight would fail those requests.
/// </remarks>
internal sealed class CustomerKeyedCache<T>(int sizeLimit, TimeSpan idleTimeout) : IDisposable
    where T : class
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = sizeLimit });
    private readonly Lock createLock = new();

    /// <summary>
    /// Get item for specified customer, creating and caching it if not found.
    /// </summary>
    public T GetOrCreate(int customer, Func<int, T> factory)
    {
        if (cache.TryGetValue<Lazy<T>>(customer, out var cachedItem)) return cachedItem!.Value;

        // Creation is locked, rather than relying on GetOrCreate, so that concurrent requests for a customer result
        // in a single item - a duplicate would mean an additional STS session
        lock (createLock)
        {
            var lazyItem = cache.GetOrCreate(customer, entry =>
            {
                entry.SetSize(1).SetSlidingExpiration(idleTimeout);
                return new Lazy<T>(() => factory(customer), LazyThreadSafetyMode.ExecutionAndPublication);
            })!;

            return lazyItem.Value;
        }
    }

    public void Dispose() => cache.Dispose();
}
