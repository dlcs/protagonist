using System;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Assets;
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

    public AssetRepository(DlcsContext dlcsContext,
        IAppCache appCache,
        IOptions<CacheSettings> cacheOptions,
        ILogger<AssetRepository> logger) : base(appCache, cacheOptions, logger)
    {
        this.dlcsContext = dlcsContext;
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

            var imageStorage =
                await dlcsContext.ImageStorages.FindAsync(id, assetId.Customer, assetId.Space);
            if (imageStorage != null)
            {
                // And related ImageStorage record
                dlcsContext.Remove(imageStorage);
                var customerStorage =
                    await dlcsContext.CustomerStorages.FindAsync(assetId.Customer, assetId.Space);

                if (customerStorage != null)
                {
                    // And reduce CustomerStorage record
                    customerStorage.NumberOfStoredImages -= 1;
                    customerStorage.TotalSizeOfThumbnails -= imageStorage.ThumbnailSize;
                    customerStorage.TotalSizeOfStoredImages -= imageStorage.Size;
                }
            }

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