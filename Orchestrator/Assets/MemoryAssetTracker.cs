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
        private readonly ILogger<MemoryAssetTracker> logger;

        private static readonly TrackedAsset NullTrackedAsset = new() {AssetId = new AssetId(-1, -1, "__notfound__")};

    public MemoryAssetTracker(IAssetRepository assetRepository, IAppCache appCache,
            ILogger<MemoryAssetTracker> logger)
        {
            this.assetRepository = assetRepository;
            this.appCache = appCache;
            this.logger = logger;
        }

        public async Task<TrackedAsset?> GetAsset(AssetId assetId)
        {
            var trackedAsset = await GetTrackedAsset(assetId);
            return trackedAsset.AssetId == NullTrackedAsset.AssetId ? null : trackedAsset;
        }

        private async Task<TrackedAsset> GetTrackedAsset(AssetId assetId)
        {
            var key = $"Track:{assetId}";
            return await appCache.GetOrAddAsync(key, async entry =>
            {
                logger.LogDebug("Refreshing cache for {AssetId}", assetId);
                var asset = await assetRepository.GetAsset(assetId);
                if (asset != null)
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); // TODO - pull from config
                    return new TrackedAsset
                    {
                        AssetId = assetId,
                        RequiresAuth = asset.RequiresAuth,
                        Origin = asset.Origin
                    };
                }

                logger.LogInformation("Asset {AssetId} not found, caching null object", assetId);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1); // TODO - pull from config
                return NullTrackedAsset;
            });
        }
    }
}