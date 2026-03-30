using API.Features.Assets.Query;
using API.Infrastructure.Page;
using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Assets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Queues.Requests;

/// <summary>
/// Get details of images within batch. This uses BatchAssets table to get historical data, not just current data 
/// </summary>
public class GetBatchAssets(int customerId, int batchId, AssetQueryModel assetQueryModel)
    : IRequest<FetchEntityResult<PageOf<Asset>>>, IPagedRequest, IOrderableRequest,
        IAssetFilterableRequest
{
    public int CustomerId { get; } = customerId;

    public int BatchId { get; } = batchId;

    public AssetQueryModel AssetQueryModel { get; } = assetQueryModel;

    public int Page { get; set; }

    public int PageSize { get; set; }

    public string? Field { get; set; }

    public bool Descending { get; set; }
}

public class GetBatchAssetsHandler(DlcsContext dlcsContext) : GetBatchAssetsBase<GetBatchAssets>(dlcsContext)
{
    protected override IQueryable<Asset> GetBatchAssets(DlcsContext dlcsContext, GetBatchAssets request)
        => dlcsContext.BatchAssets
            .AsNoTracking()
            .Include(ba => ba.Batch)
            .Include(ba => ba.Asset)
            .ThenInclude(a => a.ImageDeliveryChannels.OrderBy(idc => idc.Channel))
            .ThenInclude(dc => dc.DeliveryChannelPolicy)
            .Include(ba => ba.Asset)
            .IncludeRelated(request.AssetQueryModel.Include)
            .Where(ba => ba.Batch.Id == request.BatchId && ba.Batch.Customer == request.CustomerId)
            .Select(ba => ba.Asset);
}
