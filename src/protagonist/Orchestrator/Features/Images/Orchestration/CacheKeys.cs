using DLCS.Core.Types;

namespace Orchestrator.Features.Images.Orchestration;

internal static class CacheKeys
{
    /// <summary>
    /// Get cache key for orchestration status for asset.
    /// The existence of this key means that the item has been orchestrated
    /// </summary>
    public static string GetOrchestrationCacheKey(AssetId assetId) => $"orch:{assetId}";
}