using System;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using LazyCache;

namespace Orchestrator.ReverseProxy
{
    /// <summary>
    /// Interface for tracking the location and status of assets for orchestration.
    /// </summary>
    public interface IAssetTracker
    {
        Task<TrackedAsset> GetAsset(AssetId assetId);
    }

    /// <summary>
    /// <see cref="IAssetTracker"/> implementation using in-memory tracking
    /// </summary>
    public class MemoryAssetTracker : IAssetTracker
    {
        private readonly IAssetRepository assetRepository;
        private readonly IAppCache appCache;

        public MemoryAssetTracker(IAssetRepository assetRepository, IAppCache appCache)
        {
            this.assetRepository = assetRepository;
            this.appCache = appCache;
        }

        public Task<TrackedAsset> GetAsset(AssetId assetId)
            => GetTrackedAsset(assetId.ToString());

        private async Task<TrackedAsset> GetTrackedAsset(string assetId)
        {
            var key = $"Track:{assetId}";
            return await appCache.GetOrAddAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); // TODO - pull from config
                var asset = await assetRepository.GetAsset(assetId);
                return new TrackedAsset
                {
                    AssetId = assetId,
                    RequiresAuth = asset.RequiresAuth
                };
            });
        }
    }

    /// <summary>
    /// Represents an asset during orchestration.
    /// </summary>
    public class TrackedAsset
    {
        public string AssetId { get; set; }
        public bool RequiresAuth { get; set; }
        
        // TODO - this will manage the state of the Asset (Orchestrated, Orchestrating, Not-Orchestrated)
    }
}