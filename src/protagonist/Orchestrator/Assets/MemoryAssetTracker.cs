using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using LazyCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Images.Orchestration.Status;

namespace Orchestrator.Assets;

/// <summary>
/// <see cref="IAssetTracker"/> implementation using in-memory cache
/// </summary>
public class MemoryAssetTracker : IAssetTracker
{
    private readonly IAssetRepository assetRepository;
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly IThumbRepository thumbRepository;
    private readonly IImageOrchestrationStatusProvider statusProvider;
    private readonly ILogger<MemoryAssetTracker> logger;

    // Null object to store in cache for short duration
    private static readonly OrchestrationAsset NullOrchestrationAsset =
        new() { AssetId = new AssetId(-1, -1, "__notfound__") };

    public MemoryAssetTracker(
        IAssetRepository assetRepository,
        IAppCache appCache,
        IThumbRepository thumbRepository,
        IImageOrchestrationStatusProvider statusProvider,
        IOptions<CacheSettings> cacheOptions,
        ILogger<MemoryAssetTracker> logger)
    {
        this.assetRepository = assetRepository;
        this.appCache = appCache;
        this.thumbRepository = thumbRepository;
        this.statusProvider = statusProvider;
        this.logger = logger;
        cacheSettings = cacheOptions.Value;
    }

    public async Task<OrchestrationAsset?> GetOrchestrationAsset(AssetId assetId)
    {
        var trackedAsset = await GetOrchestrationAssetInternal(assetId, false);
        return IsNullAsset(trackedAsset) ? null : trackedAsset;
    }

    public async Task<T?> GetOrchestrationAsset<T>(AssetId assetId, bool requireOrchestrationStatus = false)
        where T : OrchestrationAsset
    {
        var trackedAsset = await GetOrchestrationAssetInternal(assetId, requireOrchestrationStatus);
        if (IsNullAsset(trackedAsset)) return null;

        if (trackedAsset is T typedAsset) return typedAsset;
        
        logger.LogWarning("Request for asset {AssetId} is of wrong type. Expected '{Expected}' but found '{Actual}",
            assetId, typeof(T), trackedAsset.GetType());
        return null;
    }

    public async Task<(bool success, OrchestrationImage latestVersion)> TrySetOrchestrationStatus(
        OrchestrationImage orchestrationImage, OrchestrationStatus status, bool force = false,
        CancellationToken cancellationToken = default)
    {
        // NOTE - there is no locking here as this is called from lock in Orchestrator
        var current = await GetOrchestrationAsset<OrchestrationImage>(orchestrationImage.AssetId);
        current.ThrowIfNull(nameof(current));

        if (current.Status == status) return (true, current);

        if (!force && current.Version > orchestrationImage.Version)
        {
            logger.LogDebug("{SaveVersion} of {AssetId} is earlier than {CurrentVersion} save failed",
                orchestrationImage.Version, orchestrationImage.AssetId, current.Version);
            return (false, current);
        }

        current.Status = status;
        current.Version += 1;

        AddToCache(current);

        return (true, current);
    }

    public async Task<T?> RefreshCachedAsset<T>(AssetId assetId, bool requireOrchestrationStatus = false)
        where T : OrchestrationAsset
    {
        // NOTE - there is no locking here as this is called from lock when orchestrating
        var cacheKey = GetCacheKey(assetId);

        var newOrchestrationAsset = await GetOrchestrationAssetFromSource(assetId, requireOrchestrationStatus);

        var current = await appCache.GetAsync<OrchestrationAsset>(cacheKey);
        newOrchestrationAsset.Version = IsNullAsset(current) ? 0 : current.Version + 1;
        AddToCache(newOrchestrationAsset);

        return newOrchestrationAsset as T;
    }

    private async Task<OrchestrationAsset> GetOrchestrationAssetInternal(AssetId assetId, bool requireOrchestrationStatus)
    {
        var key = GetCacheKey(assetId);
        var cachedAsset = await appCache.GetOrAddAsync(key, async entry =>
        {
            logger.LogTrace("Refreshing cache for {AssetId}", assetId);
            var orchestrationAsset = await GetOrchestrationAssetFromSource(assetId, requireOrchestrationStatus);
            if (orchestrationAsset != null)
            {
                return orchestrationAsset;
            }

            logger.LogDebug("Asset {AssetId} not found, caching null object", assetId);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
            return NullOrchestrationAsset;
        }, cacheSettings.GetMemoryCacheOptions());

        if (!requireOrchestrationStatus) return cachedAsset;
        
        var statusUpdated = EnsureOrchestrationStatus(cachedAsset);
        if (statusUpdated)
        {
            AddToCache(cachedAsset);
        }

        return cachedAsset;
    }
    
    private void AddToCache(OrchestrationAsset orchestrationAsset)
    {
        var cacheKey = GetCacheKey(orchestrationAsset.AssetId);
        appCache.Add(cacheKey, orchestrationAsset, cacheSettings.GetMemoryCacheOptions());
    }

    private async Task<OrchestrationAsset?> GetOrchestrationAssetFromSource(AssetId assetId, 
        bool requireOrchestrationStatus)
    {
        var asset = await assetRepository.GetAsset(assetId);
        if (asset == null || asset.NotForDelivery) return null;
        
        var orchestrationAsset = await ConvertAssetToTrackedAsset(assetId, asset, requireOrchestrationStatus);
        return orchestrationAsset;
    }

    // Most requests don't care about the OrchestrationStatus - it's only required when proxying to image-server.
    private bool EnsureOrchestrationStatus(OrchestrationAsset orchestrationAsset)
    {
        if (orchestrationAsset is OrchestrationImage { Status: OrchestrationStatus.Unknown } orchestrationImage)
        {
            logger.LogDebug("Setting orchestration status for {AssetId}", orchestrationAsset.AssetId);
            orchestrationImage.Status = statusProvider.GetOrchestrationStatus(orchestrationImage.AssetId);
            return true;
        }

        return false;
    }

    private static string GetCacheKey(AssetId assetId) => $"Track:{assetId}";

    private bool IsNullAsset(OrchestrationAsset? orchestrationAsset)
        => orchestrationAsset == null || orchestrationAsset.AssetId == NullOrchestrationAsset.AssetId;

    private async Task<OrchestrationAsset> ConvertAssetToTrackedAsset(AssetId assetId, Asset asset,
        bool requireOrchestrationStatus)
    {
        T SetDefaults<T>(T orchestrationAsset)
            where T : OrchestrationAsset
        {
            orchestrationAsset.AssetId = assetId;
            orchestrationAsset.Roles = asset.RolesList.ToList();
            orchestrationAsset.RequiresAuth = asset.RequiresAuth;
            return orchestrationAsset;
        }

        switch (asset.Family)
        {
            case AssetFamily.Image:
                var getImageLocation = assetRepository.GetImageLocation(assetId);
                var getOpenThumbs = thumbRepository.GetOpenSizes(assetId);

                await Task.WhenAll(getImageLocation, getOpenThumbs);

                var orchestrationStatus = requireOrchestrationStatus
                    ? statusProvider.GetOrchestrationStatus(assetId)
                    : OrchestrationStatus.Unknown;

                return SetDefaults(new OrchestrationImage
                {
                    S3Location = getImageLocation.Result?.S3, // TODO - error handling
                    Width = asset.Width ?? 0,
                    Height = asset.Height ?? 0,
                    MaxUnauthorised = asset.MaxUnauthorised ?? 0,
                    OpenThumbs = getOpenThumbs.Result, // TODO - reorganise thumb layout + create missing eventually
                    Status = orchestrationStatus
                });
            case AssetFamily.File:
                return SetDefaults(new OrchestrationFile { Origin = asset.Origin, });
            default:
                return SetDefaults(new OrchestrationAsset());
        }
    }
}