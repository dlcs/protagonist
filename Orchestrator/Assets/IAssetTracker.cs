using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;

namespace Orchestrator.Assets
{
    /// <summary>
    /// Interface for tracking the location and status of assets for orchestration.
    /// </summary>
    public interface IAssetTracker
    {
        /// <summary>
        /// Get <see cref="OrchestrationAsset"/> for specified AssetId
        /// </summary>
        /// <param name="assetId">Id of asset to get data for.</param>
        /// <returns>Orchestration asset details</returns>
        Task<OrchestrationAsset?> GetOrchestrationAsset(AssetId assetId);

        /// <summary>
        /// Get typed <see cref="OrchestrationAsset"/> for specified AssetId.
        /// </summary>
        /// <param name="assetId">Id of asset to get data for.</param>
        /// <typeparam name="T">Type of <see cref="OrchestrationAsset"/> to return</typeparam>
        /// <returns>Orchestration asset details</returns>
        Task<T?> GetOrchestrationAsset<T>(AssetId assetId)
            where T : OrchestrationAsset;

        /// <summary>
        /// Set the orchestration status for specified asset, if current version.
        /// </summary>
        /// <param name="orchestrationImage">OrchestrationImage to set status of</param>
        /// <param name="status">Status to set</param>
        /// <param name="force">If true save is force, else it will fail if versions don't match</param>
        /// <param name="cancellationToken">Async CancellationToken</param>
        /// <returns>boolean indicating success or failure. Failure will occur of passed image is of a lower
        /// version than cached version. Also returns latest OrchestrationImage</returns>
        Task<(bool success, OrchestrationImage latestVersion)> TrySetOrchestrationStatus(
            OrchestrationImage orchestrationImage, OrchestrationStatus status, bool force = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Refresh the cached OrchestrationAsset
        /// </summary>
        /// <param name="assetId">Id of asset to get data for.</param>
        /// <param name="cancellationToken">Async CancellationToken</param>
        /// <returns>Updated OrchestrationAsset</returns>
        Task<OrchestrationAsset> RefreshCachedAsset(AssetId assetId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Possible states for assets that can be orchestrated
    /// </summary>
    /// <remarks>This is currently relevant to images only</remarks>
    public enum OrchestrationStatus
    {
        /// <summary>
        /// Default value, status not known
        /// </summary>
        Unknown = 0,
        
        /// <summary>
        /// Asset has not been orchestrated and is still at Origin
        /// </summary>
        NotOrchestrated = 1,
        
        /// <summary>
        /// Asset is currently being orchestrated
        /// </summary>
        Orchestrating = 2,
        
        /// <summary>
        /// Asset has been orchestrated and is in local storage
        /// </summary>
        Orchestrated = 3
    }
}