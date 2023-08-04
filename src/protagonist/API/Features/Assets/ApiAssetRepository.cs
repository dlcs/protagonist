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
    
    public Task<DeleteEntityResult<Asset>> DeleteAsset(AssetId assetId) => assetRepository.DeleteAsset(assetId);
    
    /// <summary>
    /// Save changes to Asset, incrementing EntityCounters if required.
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

        await dlcsContext.SaveChangesAsync(cancellationToken);

        if (assetRepository is AssetRepositoryCachingBase cachingBase)
        {
            cachingBase.FlushCache(asset.Id);
        }

        return asset;
    }
}