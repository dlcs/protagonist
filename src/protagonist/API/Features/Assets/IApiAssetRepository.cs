using DLCS.Core.Types;
using DLCS.Model;
using DLCS.Model.Assets;

namespace API.Features.Assets;

/// <summary>
/// Asset repository containing required operations for API use
/// </summary>
public interface IApiAssetRepository
{
    /// <summary>
    /// Get specified asset and associated ImageDeliveryChannels from database
    /// </summary>
    /// <param name="assetId">Id of Asset to load</param>
    /// <param name="forUpdate">Whether this is to be updated, will use change-tracking if so</param>
    /// <param name="noCache">If true the object will not be loaded from cache</param>
    /// <returns><see cref="Asset"/> if found, or null</returns>
    public Task<Asset?> GetAsset(AssetId assetId, bool forUpdate = false, bool noCache = false);
    
    /// <summary>
    /// Delete asset and associated records from database
    /// </summary>
    /// <param name="assetId">Id of Asset to delete</param>
    /// <returns><see cref="DeleteEntityResult{Asset}"/> indicating success or failure</returns>
    public Task<DeleteEntityResult<Asset>> DeleteAsset(AssetId assetId);
    
    /// <summary>
    /// Save changes to database. This assumes provided asset is in change tracking for underlying context. Will handle
    /// incrementing EntityCounters if this is a new asset.
    /// </summary>
    /// <param name="asset">Asset to be saved, needs to be in change tracking for context</param>
    /// <param name="isUpdate">If true this is an update, else it is create</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Returns asset after saving</returns>
    public Task<Asset> Save(Asset asset, bool isUpdate, CancellationToken cancellationToken);
}