using System.Threading.Tasks;
using DLCS.Core.Types;

namespace Orchestrator.Assets
{
    /// <summary>
    /// Interface for tracking the location and status of assets for orchestration.
    /// </summary>
    public interface IAssetTracker
    {
        Task<TrackedAsset?> GetAsset(AssetId assetId);
    }
}