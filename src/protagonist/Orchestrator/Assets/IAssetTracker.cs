using System.Threading.Tasks;
using DLCS.Core.Types;

namespace Orchestrator.Assets;

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
    /// Refresh the cached OrchestrationAsset
    /// </summary>
    /// <param name="assetId">Id of asset to get data for.</param>
    /// <typeparam name="T">Type of <see cref="OrchestrationAsset"/> to return</typeparam>
    /// <returns>Updated OrchestrationAsset</returns>
    Task<T?> RefreshCachedAsset<T>(AssetId assetId)
        where T : OrchestrationAsset;
}