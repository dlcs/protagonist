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

        private const string NullId = "__notfound__";
        private static readonly TrackedAsset NullTrackedAsset = new() {AssetId = NullId};

    public MemoryAssetTracker(IAssetRepository assetRepository, IAppCache appCache,
            ILogger<MemoryAssetTracker> logger)
        {
            this.assetRepository = assetRepository;
            this.appCache = appCache;
            this.logger = logger;
        }

        public async Task<TrackedAsset?> GetAsset(AssetId assetId)
        {
            var trackedAsset = await GetTrackedAsset(assetId.ToString());
            return trackedAsset.AssetId == NullId ? null : trackedAsset;
        }

        private async Task<TrackedAsset> GetTrackedAsset(string assetId)
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
                        RequiresAuth = asset.RequiresAuth
                    };
                }

                logger.LogInformation("Asset {AssetId} not found, caching null object", assetId);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1); // TODO - pull from config
                return NullTrackedAsset;
            });
        }
    }
}