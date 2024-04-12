using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Model.Assets.Metadata;

public interface IAssetApplicationMetadataRepository
{
    public Task<AssetApplicationMetadata> UpsertApplicationMetadata(
        AssetId assetId, string metadataType, string metadataValue, 
        CancellationToken cancellationToken = default);
}