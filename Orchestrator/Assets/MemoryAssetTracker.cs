using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Core.Threading;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository.Settings;
using LazyCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Images;
using Orchestrator.Features.Images.Orchestration.Status;

namespace Orchestrator.Assets
{
    /// <summary>
    /// <see cref="IAssetTracker"/> implementation using in-memory tracking
    /// </summary>
    public class MemoryAssetTracker : IAssetTracker
    {
        private readonly IAssetRepository assetRepository;
        private readonly IAppCache appCache;
        private readonly CacheSettings cacheSettings;
        private readonly IThumbRepository thumbRepository;
        private readonly IImageOrchestrationStatusProvider statusProvider;
        private readonly ILogger<MemoryAssetTracker> logger;
        private readonly AsyncKeyedLock asyncLocker = new();

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
            var trackedAsset = await GetOrchestrationAssetInternal(assetId);
            return IsNullAsset(trackedAsset) ? null : trackedAsset;
        }

        public async Task<T?> GetOrchestrationAsset<T>(AssetId assetId) where T : OrchestrationAsset
        {
            var trackedAsset = await GetOrchestrationAssetInternal(assetId);
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
            var cacheKey = GetCacheKey(orchestrationImage.AssetId);

            using (var updateLock = await asyncLocker.LockAsync(cacheKey, cancellationToken))
            {
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

                appCache.Add(cacheKey, current, TimeSpan.FromSeconds(cacheSettings.GetTtl()));

                return (true, current);
            }
        }

        public async Task<OrchestrationAsset> RefreshCachedAsset(AssetId assetId, CancellationToken cancellationToken = default)
        {
            var cacheKey = GetCacheKey(assetId);

            var newOrchestrationAsset = await GetOrchestrationAssetFromSource(assetId);

            using (var updateLock = await asyncLocker.LockAsync(cacheKey, cancellationToken))
            {
                var current = await appCache.GetAsync<OrchestrationAsset>(cacheKey);
                newOrchestrationAsset.Version = IsNullAsset(current) ? 0 : current.Version + 1;
                appCache.Add(cacheKey, current, TimeSpan.FromSeconds(cacheSettings.GetTtl()));
            }

            return newOrchestrationAsset;
        }

        private async Task<OrchestrationAsset> GetOrchestrationAssetInternal(AssetId assetId)
        {
            var key = GetCacheKey(assetId);
            return await appCache.GetOrAddAsync(key, async entry =>
            {
                logger.LogDebug("Refreshing cache for {AssetId}", assetId);
                var orchestrationAsset = await GetOrchestrationAssetFromSource(assetId);
                if (orchestrationAsset != null)
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl());
                    return orchestrationAsset;
                }

                logger.LogInformation("Asset {AssetId} not found, caching null object", assetId);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
                return NullOrchestrationAsset;
            });
        }

        private async Task<OrchestrationAsset?> GetOrchestrationAssetFromSource(AssetId assetId)
        {
            var asset = await assetRepository.GetAsset(assetId);
            return asset != null ? await ConvertAssetToTrackedAsset(assetId, asset) : null;
        }

        private static string GetCacheKey(AssetId assetId) => $"Track:{assetId}";

        private bool IsNullAsset(OrchestrationAsset orchestrationAsset)
            => orchestrationAsset.AssetId == NullOrchestrationAsset.AssetId;

        private async Task<OrchestrationAsset> ConvertAssetToTrackedAsset(AssetId assetId, Asset asset)
        {
            switch (asset.Family)
            {
                case 'I':
                    var getImageLocation = assetRepository.GetImageLocation(assetId);
                    var getOpenThumbs = thumbRepository.GetOpenSizes(assetId);
                    var getOrchestrationStatus = statusProvider.GetOrchestrationStatus(assetId);

                    await Task.WhenAll(getImageLocation, getOpenThumbs, getOrchestrationStatus);
                    
                    return new OrchestrationImage
                    {
                        AssetId = assetId,
                        RequiresAuth = asset.RequiresAuth,
                        S3Location = getImageLocation.Result?.S3, // TODO - error handling
                        Width = asset.Width,
                        Height = asset.Height,
                        OpenThumbs = getOpenThumbs.Result, // TODO - reorganise thumb layout + create missing eventually
                        Status = getOrchestrationStatus.Result
                    };
                case 'F':
                    return new OrchestrationFile
                    {
                        AssetId = assetId, RequiresAuth = asset.RequiresAuth, Origin = asset.Origin,
                    };
                default:
                    return new OrchestrationAsset { AssetId = assetId, RequiresAuth = asset.RequiresAuth, };
            }
        }
    }
}