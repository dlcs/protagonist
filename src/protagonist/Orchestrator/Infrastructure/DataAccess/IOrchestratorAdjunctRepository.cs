using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace Orchestrator.Infrastructure.DataAccess;

public interface IOrchestratorAdjunctRepository
{
    /// <summary>
    /// Get specified adjunct from database
    /// </summary>
    /// <param name="adjunctId">Id of the adjunct to retrieve</param>
    /// <param name="assetId">Id of the parent asset of the adjunct</param>
    /// <param name="noCache">If true, the object will not be loaded from cache</param>
    /// <returns><see cref="Adjunct"/> if found, otherwise null</returns>
    public Task<Adjunct?> GetAdjunct(string adjunctId, AssetId assetId, bool noCache);
}
