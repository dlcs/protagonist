using DLCS.Core.Types;

namespace CleanupHandler.Repository;

public interface ICleanupHandlerAssetRepository
{
    /// <summary>
    /// Check whether an asset exists in the database
    /// </summary>
    Task<bool> CheckExists(AssetId assetId);
}