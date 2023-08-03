using System;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository.Entities;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Assets;

/// <summary>
/// Implementation of <see cref="IAssetRepository"/> using EFCore for data access.
/// </summary>
public class AssetRepository : AssetRepositoryCachingBase
{
    private readonly DlcsContext dlcsContext;
    private readonly IEntityCounterRepository entityCounterRepository;

    public AssetRepository(DlcsContext dlcsContext,
        IAppCache appCache,
        IEntityCounterRepository entityCounterRepository,
        IOptions<CacheSettings> cacheOptions,
        ILogger<AssetRepository> logger) : base(appCache, cacheOptions, logger)
    {
        this.dlcsContext = dlcsContext;
        this.entityCounterRepository = entityCounterRepository;
    }

    public override async Task<ImageLocation?> GetImageLocation(AssetId assetId)
        => await dlcsContext.ImageLocations.FindAsync(assetId.ToString());

    protected override async Task<DeleteEntityResult<Asset>> DeleteAssetFromDatabase(AssetId assetId)
    {
        try
        {
            var asset = await dlcsContext.Images.SingleOrDefaultAsync(i => i.Id == assetId);
            if (asset == null)
            {
                Logger.LogDebug("Attempt to delete non-existent asset {AssetId}", assetId);
                return new DeleteEntityResult<Asset>(DeleteResult.NotFound);
            }
            
            // Delete Asset
            dlcsContext.Images.Remove(asset);

            // And related ImageLocation
            var imageLocation = new ImageLocation { Id = assetId };
            dlcsContext.ImageLocations.Attach(imageLocation);
            dlcsContext.ImageLocations.Remove(imageLocation);
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
                Logger.LogInformation("No ImageStorage record found when deleting asset {AssetId}", assetId);
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
            return new DeleteEntityResult<Asset>(DeleteResult.Deleted, asset);
        }
        catch (DbUpdateConcurrencyException dbEx)
        {
            bool notFound = true;
            foreach (var entry in dbEx.Entries)
            {
                var databaseValues = await entry.GetDatabaseValuesAsync();
                if (databaseValues != null)
                {
                    notFound = false;
                }
            }

            if (notFound)
            {
                return new DeleteEntityResult<Asset>(DeleteResult.NotFound);
            }
            else
            {
                Logger.LogError(dbEx, "Concurrency exception deleting Asset {AssetId}", assetId);
                return new DeleteEntityResult<Asset>(DeleteResult.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting asset {AssetId}", assetId);
            return new DeleteEntityResult<Asset>(DeleteResult.Error);
        }
    }

    protected override async Task<Asset?> GetAssetFromDatabase(AssetId assetId) =>
        await dlcsContext.Images.AsNoTracking().SingleOrDefaultAsync(i => i.Id == assetId);
}