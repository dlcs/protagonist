using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace CleanupHandler.Repository;

public interface ICleanupHandlerAssetRepository
{
    /// <summary>
    /// Retrieves an asset from the database with attached delivery channel policies
    /// </summary>
    /// <param name="assetId">The asset id to retrieve details for</param>
    /// <returns>an asset</returns>
    Task<Asset?> RetrieveAssetWithDeliveryChannels(AssetId assetId);
}