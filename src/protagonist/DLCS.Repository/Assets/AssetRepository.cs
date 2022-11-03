using System;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
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

    protected override async Task<ResultStatus<DeleteResult>> DeleteAssetFromDatabase(AssetId assetId)
    {
        try
        {
            // Delete Asset
            var toDelete = new Asset { Id = assetId };
            dlcsContext.Images.Attach(toDelete);
            dlcsContext.Images.Remove(toDelete);

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
                return ResultStatus<DeleteResult>.Unsuccessful(DeleteResult.NotFound);
            }
            
            await entityCounterRepository.Decrement(customer, "space-images", space.ToString());
            await entityCounterRepository.Decrement(0, "customer-images", customer.ToString());
            return ResultStatus<DeleteResult>.Successful(DeleteResult.Deleted);
        }
        catch (DbUpdateConcurrencyException dbEx)
        {
            bool notFound = true;
            foreach (var entry in dbEx.Entries)
            {
                var databaseValues = entry.GetDatabaseValues();
                if (databaseValues != null)
                {
                    notFound = false;
                }
            }

            if (notFound)
            {
                return ResultStatus<DeleteResult>.Unsuccessful(DeleteResult.NotFound);
            }
            else
            {
                Logger.LogError(dbEx, "Concurrency exception deleting Asset {AssetId}", assetId);
                return ResultStatus<DeleteResult>.Unsuccessful(DeleteResult.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting asset {AssetId}", assetId);
            return ResultStatus<DeleteResult>.Unsuccessful(DeleteResult.Error);
        }
    }

    protected override async Task<Asset?> GetAssetFromDatabase(AssetId assetId) =>
        await dlcsContext.Images.AsNoTracking().SingleOrDefaultAsync(i => i.Id == assetId);
}