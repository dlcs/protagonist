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

        public MemoryAssetTracker(IAssetRepository assetRepository, IAppCache appCache,
            ILogger<MemoryAssetTracker> logger)
        {
            this.assetRepository = assetRepository;
            this.appCache = appCache;
            this.logger = logger;
        }

        public Task<TrackedAsset> GetAsset(AssetId assetId)
            => GetTrackedAsset(assetId.ToString());

        private async Task<TrackedAsset> GetTrackedAsset(string assetId)
        {
            var key = $"Track:{assetId}";
            return await appCache.GetOrAddAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); // TODO - pull from config
                logger.LogDebug("Refreshing cache for {AssetId}", assetId);
                var asset = await assetRepository.GetAsset(assetId);
                return new TrackedAsset
                {
                    AssetId = assetId,
                    RequiresAuth = asset.RequiresAuth
                };
            });
        }
    }
}