using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Model.Assets.Metadata;

public interface IAssetApplicationMetadataRepository
{
    /// <summary>
    /// Deletes asset application metadata from the table
    /// </summary>
    /// <param name="assetId">The asset id associated with the metadata</param>
    /// <param name="metadataType">The type of metadata to delete</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A boolean value based on successful deletion</returns>
    public Task<bool> DeleteAssetApplicationMetadata(AssetId assetId, string metadataType,
        CancellationToken cancellationToken = default);
}
