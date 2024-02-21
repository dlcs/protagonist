using System;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model;
using DLCS.Model.Assets;
using LazyCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Assets;

/// <summary>
/// Base AssetRepository that manages caching/clearing/deleting items from underlying cache.
/// </summary>
public abstract class AssetRepositoryCachingBase : IAssetRepository
{
    protected readonly IAppCache AppCache;
    protected readonly ILogger Logger;
    protected readonly CacheSettings CacheSettings;
    private static readonly Asset NullAsset = new() { Id = AssetId.Null };

    public AssetRepositoryCachingBase(IAppCache appCache, IOptions<CacheSettings> cacheOptions, ILogger logger)
    {
        this.AppCache = appCache;
        this.Logger = logger;
        CacheSettings = cacheOptions.Value;
    }
    
    public Task<Asset?> GetAsset(AssetId id) => GetAssetInternal(id);

    public Task<Asset?> GetAsset(AssetId id, bool noCache) => GetAssetInternal(id, noCache);

    public abstract Task<ImageLocation?> GetImageLocation(AssetId assetId);

    public Task<DeleteEntityResult<Asset>> DeleteAsset(AssetId assetId)
    {
        AppCache.Remove(GetCacheKey(assetId));
        
        return DeleteAssetFromDatabase(assetId);
    }
    
    public void FlushCache(AssetId assetId) => AppCache.Remove(GetCacheKey(assetId));

    /// <summary>
    /// Delete asset from database
    /// </summary>
    protected abstract Task<DeleteEntityResult<Asset>> DeleteAssetFromDatabase(AssetId assetId);
    
    /// <summary>
    /// Find asset in DB and materialise to <see cref="Asset"/> object
    /// </summary>
    protected abstract Task<Asset?> GetAssetFromDatabase(AssetId assetId);

    private string GetCacheKey(AssetId assetId) => $"asset:{assetId}";

    private async Task<Asset?> GetAssetInternal(AssetId assetId, bool noCache = false)
    {
        var key = GetCacheKey(assetId);
        
        if (noCache)
        {
            AppCache.Remove(key);
        }

        var asset = await AppCache.GetOrAddAsync(key, async entry =>
        {
            Logger.LogDebug("Refreshing assetCache from database {Asset}", assetId);
            var dbAsset = await GetAssetFromDatabase(assetId);
            if (dbAsset == null)
            {
                entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromSeconds(CacheSettings.GetTtl(CacheDuration.Short));
                return NullAsset;
            }

            return dbAsset;
        }, CacheSettings.GetMemoryCacheOptions());
        
        return asset.Id == NullAsset.Id ? null : asset;
    }
}