using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;

namespace API.Features.Assets;

/// <summary>
/// Extends basic <see cref="IAssetRepository"/> to include some API specific methods
/// </summary>
public interface IApiAssetRepository : IAssetRepository
{
    public Task<PageOfAssets?> GetPageOfAssets(int customerId, int spaceId, int page, int pageSize, string? orderBy,
        bool descending, AssetFilter? assetFilter, CancellationToken cancellationToken);

    public Task<Asset> Save(Asset asset, CancellationToken cancellationToken);
}