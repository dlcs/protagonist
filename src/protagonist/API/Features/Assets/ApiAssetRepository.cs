using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Assets;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Assets;

/// <summary>
/// Asset repository, extends base <see cref="IAssetRepository"/> with custom, API-specific methods.
/// </summary>
public class ApiAssetRepository : IApiAssetRepository
{
    private readonly IAssetRepository assetRepository;
    private readonly DlcsContext dlcsContext;

    public ApiAssetRepository(
        DlcsContext dlcsContext,
        IAssetRepository assetRepository)
    {
        this.dlcsContext = dlcsContext;
        this.assetRepository = assetRepository;
    }

    public Task<Asset?> GetAsset(string id) => assetRepository.GetAsset(id);

    public Task<Asset?> GetAsset(AssetId id) => assetRepository.GetAsset(id);

    public Task<Asset?> GetAsset(string id, bool noCache) => assetRepository.GetAsset(id, noCache);

    public Task<Asset?> GetAsset(AssetId id, bool noCache) => assetRepository.GetAsset(id, noCache);

    public Task<ImageLocation?> GetImageLocation(AssetId assetId) => assetRepository.GetImageLocation(assetId);
    
    public Task<ResultStatus<DeleteResult>> DeleteAsset(AssetId assetId) => assetRepository.DeleteAsset(assetId);

    public async Task<PageOfAssets?> GetPageOfAssets(int customerId, int spaceId, int page, int pageSize,
        string? orderBy, bool descending, AssetFilter? assetFilter, CancellationToken cancellationToken)
    {
        var space = await dlcsContext.Spaces.SingleOrDefaultAsync(
            s => s.Customer == customerId && s.Id == spaceId, cancellationToken: cancellationToken);
        if (space == null)
        {
            return null;
        }

        var result = new PageOfAssets
        {
            Page = page,
            Total = await dlcsContext.Images.CountAsync(
                a => a.Customer == customerId && a.Space == spaceId, cancellationToken: cancellationToken),
            Assets = await dlcsContext.Images.AsNoTracking()
                .Where(a => a.Customer == customerId && a.Space == spaceId)
                .ApplyAssetFilter(assetFilter, false)
                .AsOrderedAssetQuery(orderBy, descending)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken: cancellationToken)
        };
        return result;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asset">
    /// An Asset that is ready to be inserted/updated in the DB, that
    /// has usually come from an incoming Hydra object.
    /// It can also have been obtained from the database by another repository class.
    /// </param>
    /// <param name="cancellationToken"></param>
    public async Task<Asset> Save(Asset asset, CancellationToken cancellationToken)
    {
        if (dlcsContext.Images.Local.All(trackedAsset => trackedAsset.Id != asset.Id))
        {
            await dlcsContext.Images.AddAsync(asset, cancellationToken);
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
        await dlcsContext.Entry(asset).ReloadAsync(cancellationToken);

        return asset;
    }
}