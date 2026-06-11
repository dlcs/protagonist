using System.Threading.Tasks;
using DLCS.Core.Types;

namespace Orchestrator.Assets;

/// <summary>
/// Interface for tracking the location and status of adjuncts for orchestration
/// </summary>
public interface IAdjunctTracker
{
    /// <summary>
    /// Get <see cref="OrchestrationAdjunct"/> for specified AssetId
    /// </summary>
    /// <param name="adjunctId">Id of the adjunct to get data for</param>
    /// <param name="assetId">Id of asset the adjunct belongs to</param>
    /// <returns>Orchestration asset details</returns>
    Task<OrchestrationAdjunct?> GetOrchestrationAdjunct(string adjunctId, AssetId assetId);

    /// <summary>
    /// Refresh the cached OrchestrationAdjunct
    /// </summary>
    /// <param name="adjunctId">Id of the adjunct to get data for</param>
    /// <param name="assetId">Id of asset the adjunct belongs to</param>
    /// <returns>Updated OrchestrationAdjunct</returns>
    Task<OrchestrationAdjunct?> RefreshCachedAdjunct(string adjunctId, AssetId assetId);
}
