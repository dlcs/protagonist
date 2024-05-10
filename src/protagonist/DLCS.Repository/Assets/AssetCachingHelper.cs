using System;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using LazyCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Assets;

/// <summary>
/// Helper for working with cached assets
/// </summary>
public class AssetCachingHelper
{
    private readonly IAppCache appCache;
    private readonly ILogger<AssetCachingHelper> logger;
    private readonly CacheSettings cacheSettings;
    private static readonly Asset NullAsset = new() { Id = AssetId.Null };

    public AssetCachingHelper(IAppCache appCache, IOptions<CacheSettings> cacheOptions,
        ILogger<AssetCachingHelper> logger)
    {
        this.appCache = appCache;
        this.logger = logger;
        cacheSettings = cacheOptions.Value;
    }

    /// <summary>
    /// Purge specified asset from cache
    /// </summary>
    public void RemoveAssetFromCache(AssetId assetId) => appCache.Remove(GetCacheKey(assetId));

    /// <summary>
    /// Use provided assetLoader function to load asset from underlying data source. Will cache null values for a short
    /// duration.
    /// </summary>
    public async Task<Asset?> GetCachedAsset(AssetId assetId, Func<AssetId, Task<Asset?>> assetLoader,
        CacheDuration cacheDuration = CacheDuration.Default)
    {
        var key = GetCacheKey(assetId);

        var asset = await appCache.GetOrAddAsync(key, async entry =>
        {
            logger.LogDebug("Refreshing assetCache from database {Asset}", assetId);
            var dbAsset = await assetLoader(assetId);
            if (dbAsset == null)
            {
                entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
                return NullAsset;
            }

            return dbAsset;
        }, cacheSettings.GetMemoryCacheOptions(cacheDuration));

        return asset.Id == NullAsset.Id ? null : asset;
    }

    private string GetCacheKey(AssetId assetId) => $"asset:{assetId}";
}