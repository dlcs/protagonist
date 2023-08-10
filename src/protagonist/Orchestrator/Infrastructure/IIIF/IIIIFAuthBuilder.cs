using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using IIIF;

namespace Orchestrator.Infrastructure.IIIF;

/// <summary>
/// Basic interface for getting auth services for an asset
/// </summary>
public interface IIIIFAuthBuilder
{
    /// <summary>
    /// Generate a IIIF <see cref="IService"/> for authorisation services for specified asset.
    /// </summary>
    /// <returns><see cref="IService"/> if found, else null</returns>
    Task<IService?> GetAuthServicesForAsset(AssetId assetId, List<string> roles,
        CancellationToken cancellationToken = default);
}