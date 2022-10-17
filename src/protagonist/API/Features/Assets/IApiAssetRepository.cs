using DLCS.Model.Assets;

namespace API.Features.Assets;

/// <summary>
/// Extends basic <see cref="IAssetRepository"/> to include some API specific methods
/// </summary>
public interface IApiAssetRepository : IAssetRepository
{
    public Task<Asset> Save(Asset asset, bool isUpdate, CancellationToken cancellationToken);
}