using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace Orchestrator.Infrastructure.DataAccess;

public interface IOrchestratorAssetRepository : IAssetRepository
{
    /// <summary>
    /// Get specified asset from database
    /// </summary>
    /// <param name="assetId">Id of Asset to load</param>
    /// <param name="noCache">If true the object will not be loaded from cache</param>
    /// <returns><see cref="Asset"/> if found, or null</returns>
    public Task<Asset?> GetAsset(AssetId assetId, bool noCache);
}
