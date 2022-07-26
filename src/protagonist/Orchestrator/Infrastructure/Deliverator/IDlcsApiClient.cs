using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;

namespace Orchestrator.Infrastructure.Deliverator;

public interface IDlcsApiClient
{
    /// <summary>
    /// Call dlcs API to reingest specified asset
    /// </summary>
    /// <param name="assetId">Id of asset to reingest</param>
    /// <returns>true if reingest success, else false</returns>
    Task<bool> ReingestAsset(AssetId assetId, CancellationToken cancellationToken = default);
}