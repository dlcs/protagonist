using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Assets;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Queues.Requests;

/// <summary>
/// Get details of images within batch. This will include images currently in that batch only
/// </summary>
/// <remarks>
/// Although the behaviour is slightly different, this has been superseded by <see cref="GetBatchAssets"/>, which
/// returns historical data as well as current batch data
/// </remarks>
public class GetBatchImages : GetBatchAssets
{
    public GetBatchImages(int customerId, int batchId, AssetFilter? assetFilter)
        : base(customerId, batchId, assetFilter)
    {
    }
}

public class GetBatchImagesHandler : GetBatchAssetsBase<GetBatchImages>
{
    public GetBatchImagesHandler(DlcsContext dlcsContext) : base(dlcsContext)
    {
    }

    protected override IQueryable<Asset> GetBatchAssets(DlcsContext dlcsContext, GetBatchImages request)
        => dlcsContext.Images
            .AsNoTracking()
            .IncludeDeliveryChannelsWithPolicy()
            .Where(a => a.Customer == request.CustomerId && a.Batch == request.BatchId);
}