using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Model.Assets.Metadata;

public interface IAssetApplicationMetadataRepository
{
    public Task<List<int[]>> GetThumbnailSizes(AssetId assetId);

    public Task<AssetApplicationMetadata> AddApplicationMetadata(
        AssetApplicationMetadata metadata, 
        CancellationToken cancellationToken = default);
}