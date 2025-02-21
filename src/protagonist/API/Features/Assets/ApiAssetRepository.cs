using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Assets;

/// <summary>
/// API specific asset repository
/// </summary>
public class ApiAssetRepository : IApiAssetRepository
{
    private readonly IEntityCounterRepository entityCounterRepository;
    private readonly AssetCachingHelper assetCachingHelper;
    private readonly DlcsContext dlcsContext;
    private readonly ILogger<ApiAssetRepository> logger;

    public ApiAssetRepository(
        DlcsContext dlcsContext,
        IEntityCounterRepository entityCounterRepository,
        AssetCachingHelper assetCachingHelper, 
        ILogger<ApiAssetRepository> logger)
    {
        this.dlcsContext = dlcsContext;
        this.entityCounterRepository = entityCounterRepository;
        this.assetCachingHelper = assetCachingHelper;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<Asset?> GetAsset(AssetId assetId, bool forUpdate = false, bool noCache = false)
    {
        // Only use change-tracking if this will be used for an update operation
        IQueryable<Asset> images = forUpdate ? dlcsContext.Images : dlcsContext.Images.AsNoTracking();

        Task<Asset?> LoadAssetFromDb(AssetId id) =>
            images
                .IncludeDeliveryChannelsWithPolicy()
                .SingleOrDefaultAsync(i => i.Id == id);

        if (noCache) assetCachingHelper.RemoveAssetFromCache(assetId);

        // Only go via cache if this is a read-only operation
        var asset = forUpdate
            ? await LoadAssetFromDb(assetId)
            : await assetCachingHelper.GetCachedAsset(assetId, LoadAssetFromDb);
        return asset;
    }

    /// <inheritdoc />
    public async Task<DeleteEntityResult<Asset>> DeleteAsset(AssetId assetId)
    {
        try
        {
            var asset = await dlcsContext.Images
                .Include(a => a.ImageDeliveryChannels)
                .SingleOrDefaultAsync(i => i.Id == assetId);
            if (asset == null)
            {
                logger.LogDebug("Attempt to delete non-existent asset {AssetId}", assetId);
                return new DeleteEntityResult<Asset>(DeleteResult.NotFound);
            }
            
            // Delete Asset
            dlcsContext.Images.Remove(asset);

            // And related ImageLocation
            var imageLocation = await dlcsContext.ImageLocations.FindAsync(assetId);

            if (imageLocation != null)
            {
                dlcsContext.ImageLocations.Remove(imageLocation);
            }

            var customer = assetId.Customer;
            var space = assetId.Space;
            
            var imageStorage =
                await dlcsContext.ImageStorages.FindAsync(assetId, customer, space);
            if (imageStorage != null)
            {
                // And related ImageStorage record
                dlcsContext.Remove(imageStorage);
            }
            else
            {
                logger.LogInformation("No ImageStorage record found when deleting asset {AssetId}", assetId);
            }
            
            void ReduceCustomerStorage(CustomerStorage customerStorage)
            {
                // And reduce CustomerStorage record
                customerStorage.NumberOfStoredImages -= 1;
                customerStorage.TotalSizeOfThumbnails -= imageStorage?.ThumbnailSize ?? 0;
                customerStorage.TotalSizeOfStoredImages -= imageStorage?.Size ?? 0;
            }

            // Reduce CustomerStorage for space
            var customerSpaceStorage = await dlcsContext.CustomerStorages.FindAsync(customer, space);
            if (customerSpaceStorage != null) ReduceCustomerStorage(customerSpaceStorage);

            // Reduce CustomerStorage for overall customer
            var customerStorage = await dlcsContext.CustomerStorages.FindAsync(customer, 0);
            if (customerStorage != null) ReduceCustomerStorage(customerStorage);

            var rowCount = await dlcsContext.SaveChangesAsync();
            if (rowCount == 0)
            {
                return new DeleteEntityResult<Asset>(DeleteResult.NotFound);
            }
            
            await entityCounterRepository.Decrement(customer, KnownEntityCounters.SpaceImages, space.ToString());
            await entityCounterRepository.Decrement(0, KnownEntityCounters.CustomerImages, customer.ToString());
            assetCachingHelper.RemoveAssetFromCache(assetId);
            return new DeleteEntityResult<Asset>(DeleteResult.Deleted, asset);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting asset {AssetId}", assetId);
            return new DeleteEntityResult<Asset>(DeleteResult.Error);
        }
    }
    
    /// <inheritdoc />
    public async Task<Asset> Save(Asset asset, bool isUpdate, CancellationToken cancellationToken)
    {
        if (!isUpdate) // if this is a creation, add Asset to dbContext + increment entity counters
        {
            await dlcsContext.Images.AddAsync(asset, cancellationToken);
            await entityCounterRepository.Increment(asset.Customer, KnownEntityCounters.SpaceImages,
                asset.Space.ToString());
            await entityCounterRepository.Increment(0, KnownEntityCounters.CustomerImages, asset.Customer.ToString());
        }

        await dlcsContext.SaveChangesAsync(cancellationToken);
        assetCachingHelper.RemoveAssetFromCache(asset.Id);
        return asset;
    }
}