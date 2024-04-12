using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Model.Assets.Metadata;

public interface IAssetApplicationMetadataRepository
{
    /// <summary>
    /// Upserts asset application metadata into the table
    /// </summary>
    /// <param name="assetId">The asset id this metadata will relate to</param>
    /// <param name="metadataType">The type of metadata to create</param>
    /// <param name="metadataValue">The value of this metadata</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A copy of the metadata that has been put into the database</returns>
    public Task<AssetApplicationMetadata> UpsertApplicationMetadata(
        AssetId assetId, string metadataType, string metadataValue, 
        CancellationToken cancellationToken = default);
}