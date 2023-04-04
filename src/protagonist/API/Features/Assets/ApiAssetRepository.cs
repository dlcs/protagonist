using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Assets;

/// <summary>
/// Asset repository, extends base <see cref="IAssetRepository"/> with custom, API-specific methods.
/// </summary>
public class ApiAssetRepository : IApiAssetRepository
{
    private readonly IAssetRepository assetRepository;
    private readonly IEntityCounterRepository entityCounterRepository;
    private readonly DlcsContext dlcsContext;

    public ApiAssetRepository(
        DlcsContext dlcsContext,
        IAssetRepository assetRepository, 
        IEntityCounterRepository entityCounterRepository)
    {
        this.dlcsContext = dlcsContext;
        this.assetRepository = assetRepository;
        this.entityCounterRepository = entityCounterRepository;
    }

    public Task<Asset?> GetAsset(AssetId id) => assetRepository.GetAsset(id);

    public Task<Asset?> GetAsset(AssetId id, bool noCache) => assetRepository.GetAsset(id, noCache);

    public Task<ImageLocation?> GetImageLocation(AssetId assetId) => assetRepository.GetImageLocation(assetId);
    
    public Task<ResultStatus<DeleteResult>> DeleteAsset(AssetId assetId) => assetRepository.DeleteAsset(assetId);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="asset">
    /// An Asset that is ready to be inserted/updated in the DB, that
    /// has usually come from an incoming Hydra object.
    /// It can also have been obtained from the database by another repository class.
    /// </param>
    /// <param name="isUpdate">True if this is an update, false if insert</param>
    /// <param name="cancellationToken"></param>
    public async Task<Asset> Save(Asset asset, bool isUpdate, CancellationToken cancellationToken)
    {
        if (dlcsContext.Images.Local.All(trackedAsset => trackedAsset.Id != asset.Id))
        {
            if (isUpdate)
            {
                dlcsContext.Images.Attach(asset);
                dlcsContext.Entry(asset).State = EntityState.Modified;
            }
            else
            {
                await dlcsContext.Images.AddAsync(asset, cancellationToken);
                await entityCounterRepository.Increment(asset.Customer, KnownEntityCounters.SpaceImages, asset.Space.ToString());
                await entityCounterRepository.Increment(0, KnownEntityCounters.CustomerImages, asset.Customer.ToString());
            }
        }

        // In Deliverator, if this is a PATCH, the ImageLocation is simply removed.
        //  - (DeleteImageLocationBehaviour) - https://github.com/digirati-co-uk/deliverator/blob/87f6cfde97be94d2e9e00c11c4dc0fcfacfdd087/API/Architecture/Request/API/Entities/CustomerSpaceImage.cs#L554
        // but if it's a PUT, a new ImageLocation row is created.
        //  - (CreateSkeletonImageLocationBehaviour, UpdateImageLocationBehaviour) - https://github.com/digirati-co-uk/deliverator/blob/87f6cfde97be94d2e9e00c11c4dc0fcfacfdd087/API/Architecture/Request/API/Entities/CustomerSpaceImage.cs#L303

        // As a common operation, we'll just upsert an Image Location and clear its fields.
        var imageLocation = await dlcsContext.ImageLocations.FindAsync(new object[] { asset.Id }, cancellationToken);
        if (imageLocation == null)
        {
            imageLocation = new ImageLocation { Id = asset.Id };
            dlcsContext.ImageLocations.Add(imageLocation);
        }

        imageLocation.S3 = string.Empty;
        imageLocation.Nas = string.Empty;

        await dlcsContext.SaveChangesAsync(cancellationToken);

        if (assetRepository is AssetRepositoryCachingBase cachingBase)
        {
            cachingBase.FlushCache(asset.Id);
        }

        return asset;
    }
}