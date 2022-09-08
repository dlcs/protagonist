using System;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Caching;
using DLCS.Core.Types;
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
    private static readonly Asset NullAsset = new() { Id = "__nullasset__" };

    public AssetRepositoryCachingBase(IAppCache appCache, IOptions<CacheSettings> cacheOptions, ILogger logger)
    {
        this.AppCache = appCache;
        this.Logger = logger;
        CacheSettings = cacheOptions.Value;
    }

    public Task<Asset?> GetAsset(string id) => GetAssetInternal(id);

    public Task<Asset?> GetAsset(AssetId id) => GetAssetInternal(id.ToString());

    public Task<Asset?> GetAsset(string id, bool noCache) => GetAssetInternal(id, noCache);

    public Task<Asset?> GetAsset(AssetId id, bool noCache) => GetAssetInternal(id.ToString(), noCache);

    public abstract Task<ImageLocation?> GetImageLocation(AssetId assetId);

    public Task<ResultStatus<DeleteResult>> DeleteAsset(AssetId assetId)
    {
        var id = assetId.ToString();
        AppCache.Remove(GetCacheKey(id));
        
        return DeleteAssetFromDatabase(id);
    }

    /// <summary>
    /// Delete asset from database
    /// </summary>
    protected abstract Task<ResultStatus<DeleteResult>> DeleteAssetFromDatabase(string id);
    
    /// <summary>
    /// Find asset in DB and materialise to <see cref="Asset"/> object
    /// </summary>
    protected abstract Task<Asset?> GetAssetFromDatabase(string id);

    private string GetCacheKey(string id) => $"asset:{id}";

    private async Task<Asset?> GetAssetInternal(string id, bool noCache = false)
    {
        var key = GetCacheKey(id);
        
        if (noCache)
        {
            AppCache.Remove(key);
        }

        var asset = await AppCache.GetOrAddAsync(key, async entry =>
        {
            Logger.LogDebug("Refreshing assetCache from database {Asset}", id);
            var dbAsset = await GetAssetFromDatabase(id);
            if (dbAsset == null)
            {
                entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromSeconds(CacheSettings.GetTtl(CacheDuration.Short));
                return NullAsset;
            }

            return dbAsset;
        }, CacheSettings.GetMemoryCacheOptions(CacheDuration.Short));
        
        return asset.Id == NullAsset.Id ? null : asset;
    }
}