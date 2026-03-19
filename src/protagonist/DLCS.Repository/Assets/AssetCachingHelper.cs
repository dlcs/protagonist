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
public class AssetCachingHelper(
    IAppCache appCache,
    IOptions<CacheSettings> cacheOptions,
    ILogger<AssetCachingHelper> logger)
{
    private readonly CacheSettings cacheSettings = cacheOptions.Value;
    private static readonly Asset NullAsset = new() { Id = AssetId.Null };

    private static readonly Adjunct NullAdjunct = new()
    {
        Id = "null", AssetId = AssetId.Null, IIIFLink = IIIFLinkType.SeeAlso, MediaType = "null/null", Type = "null"
    };

    /// <summary>
    /// Purge specified asset from cache
    /// </summary>
    public void RemoveAssetFromCache(AssetId assetId) => appCache.Remove(GetCacheKey(assetId));

    /// <summary>
    /// Purge specified adjunct from cache
    /// </summary>
    public void RemoveAdjunctFromCache(string adjunctId, AssetId assetId) => appCache.Remove(GetCacheKey(assetId, adjunctId));
    
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
    
    /// <summary>
    /// Use provided adjunctLoader function to load adjunct from underlying data source. Will cache null values for a short
    /// duration.
    /// </summary>
    public async Task<Adjunct?> GetCachedAdjunct(string adjunctId, AssetId assetId, Func<string, AssetId, Task<Adjunct?>> adjunctLoader,
        CacheDuration cacheDuration = CacheDuration.Default)
    {
        var key = GetCacheKey(assetId, adjunctId);

        // Note that due to single/dual key discrepancy with Assets, reusing the code is more trouble than it's worth
        var adjunct = await appCache.GetOrAddAsync(key, async entry =>
        {
            logger.LogDebug("Refreshing adjunctCache from database {Adjunct} for asset {Asset}", adjunctId, assetId);
            var dbAdjunct = await adjunctLoader(adjunctId, assetId);
            if (dbAdjunct != null)
            {
                return dbAdjunct;
            }

            entry.AbsoluteExpirationRelativeToNow =
                TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
            return NullAdjunct;
        }, cacheSettings.GetMemoryCacheOptions(cacheDuration));

        return adjunct.Id == NullAdjunct.Id && adjunct.AssetId == NullAdjunct.AssetId ? null : adjunct;
    }

    private static string GetCacheKey(AssetId assetId, string? adjunctId = null)
        => $"asset:{assetId}"
            + adjunctId is { Length: > 0 }
            ? $"_adj_{adjunctId}"
            : string.Empty;
}
