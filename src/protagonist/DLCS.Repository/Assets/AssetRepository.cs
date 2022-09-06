using System;
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

    protected override async Task<ResultStatus<DeleteResult>> DeleteAssetFromDatabase(string id)
    {
        try
        {
            // Delete Asset
            var toDelete = new Asset { Id = id };
            dlcsContext.Images.Attach(toDelete);
            dlcsContext.Images.Remove(toDelete);

            // And related ImageLocation
            var imageLocation = new ImageLocation { Id = id };
            dlcsContext.ImageLocations.Attach(imageLocation);
            dlcsContext.ImageLocations.Remove(imageLocation);

            var assetId = AssetId.FromString(id);
            var customer = assetId.Customer;
            
            var imageStorage =
                await dlcsContext.ImageStorages.FindAsync(id, customer, assetId.Space);
            if (imageStorage != null)
            {
                // And related ImageStorage record
                dlcsContext.Remove(imageStorage);

                void ReduceCustomerStorage(CustomerStorage customerStorage)
                {
                    // And reduce CustomerStorage record
                    customerStorage.NumberOfStoredImages -= 1;
                    customerStorage.TotalSizeOfThumbnails -= imageStorage.ThumbnailSize;
                    customerStorage.TotalSizeOfStoredImages -= imageStorage.Size;
                }

                // Reduce CustomerStorage for space
                var customerSpaceStorage =
                    await dlcsContext.CustomerStorages.FindAsync(customer, assetId.Space);
                if (customerSpaceStorage != null) ReduceCustomerStorage(customerSpaceStorage);

                // Reduce CustomerStorage for overall customer
                var customerStorage =
                    await dlcsContext.CustomerStorages.FindAsync(customer, 0);
                if (customerStorage != null) ReduceCustomerStorage(customerStorage);
            }

            await entityCounterRepository.Decrement(customer, "space-images", customer.ToString());
            await entityCounterRepository.Decrement(0, "customer-images", customer.ToString());

            var rowCount = await dlcsContext.SaveChangesAsync();
            return rowCount == 0
                ? ResultStatus<DeleteResult>.Unsuccessful(DeleteResult.NotFound)
                : ResultStatus<DeleteResult>.Successful(DeleteResult.Deleted);
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
                Logger.LogError(dbEx, "Concurrency exception deleting Asset {AssetId}", id);
                return ResultStatus<DeleteResult>.Unsuccessful(DeleteResult.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting asset {AssetId}", id);
            return ResultStatus<DeleteResult>.Unsuccessful(DeleteResult.Error);
        }
    }

    protected override async Task<Asset?> GetAssetFromDatabase(string id)
        => await dlcsContext.Images.FindAsync(id);
}