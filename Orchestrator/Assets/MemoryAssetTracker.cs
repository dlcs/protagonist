using System;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using LazyCache;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Assets
{
    /// <summary>
    /// <see cref="IAssetTracker"/> implementation using in-memory tracking
    /// </summary>
    public class MemoryAssetTracker : IAssetTracker
    {
        private readonly IAssetRepository assetRepository;
        private readonly IAppCache appCache;
        private readonly IThumbRepository thumbRepository;
        private readonly ILogger<MemoryAssetTracker> logger;

        // Null object to store in cache for short duration
        private static readonly OrchestrationAsset NullOrchestrationAsset =
            new() { AssetId = new AssetId(-1, -1, "__notfound__") };

    public MemoryAssetTracker(IAssetRepository assetRepository, IAppCache appCache, IThumbRepository thumbRepository,
            ILogger<MemoryAssetTracker> logger)
        {
            this.assetRepository = assetRepository;
            this.appCache = appCache;
            this.thumbRepository = thumbRepository;
            this.logger = logger;
        }

        public async Task<OrchestrationAsset?> GetOrchestrationAsset(AssetId assetId)
        {
            var trackedAsset = await GetTrackedAsset(assetId);
            return IsNullAsset(trackedAsset) ? null : trackedAsset;
        }

        public async Task<T?> GetOrchestrationAsset<T>(AssetId assetId) where T : OrchestrationAsset
        {
            var trackedAsset = await GetTrackedAsset(assetId);
            if (IsNullAsset(trackedAsset)) return null;

            if (trackedAsset is T typedAsset) return typedAsset;
            
            logger.LogWarning("Request for asset {AssetId} is of wrong type. Expected '{Expected}' but found '{Actual}",
                assetId, typeof(T), trackedAsset.GetType());
            return null;
        }

        private async Task<OrchestrationAsset> GetTrackedAsset(AssetId assetId)
        {
            var key = $"Track:{assetId}";
            return await appCache.GetOrAddAsync(key, async entry =>
            {
                logger.LogDebug("Refreshing cache for {AssetId}", assetId);
                var asset = await assetRepository.GetAsset(assetId);
                if (asset != null)
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); // TODO - pull from config
                    return await ConvertAssetToTrackedAsset(assetId, asset);
                }

                logger.LogInformation("Asset {AssetId} not found, caching null object", assetId);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1); // TODO - pull from config
                return NullOrchestrationAsset;
            });
        }

        private bool IsNullAsset(OrchestrationAsset orchestrationAsset)
            => orchestrationAsset.AssetId == NullOrchestrationAsset.AssetId;

        private async Task<OrchestrationAsset> ConvertAssetToTrackedAsset(AssetId assetId, Asset asset)
            => asset.Family switch
            {
                'I' => new OrchestrationImage
                {
                    AssetId = assetId,
                    RequiresAuth = asset.RequiresAuth,
                    Origin = asset.Origin,
                    Width = asset.Width,
                    Height = asset.Height,
                    OpenThumbs = await thumbRepository.GetOpenSizes(assetId)
                },
                _ => new OrchestrationAsset
                {
                    AssetId = assetId,
                    RequiresAuth = asset.RequiresAuth,
                    Origin = asset.Origin,
                }
            };
    }
}