using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Model.Page;
using DLCS.Repository;
using DLCS.Repository.Assets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Queues.Requests;

/// <summary>
/// Get details of images within batch. This uses BatchAssets table to get historical data, not just current data 
/// </summary>
public class GetBatchAssets : IRequest<FetchEntityResult<PageOf<Asset>>>, IPagedRequest, IOrderableRequest,
    IAssetFilterableRequest
{
    public int CustomerId { get; }

    public int BatchId { get; }

    public AssetFilter? AssetFilter { get; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public string? Field { get; set; }

    public bool Descending { get; set; }

    public GetBatchAssets(int customerId, int batchId, AssetFilter? assetFilter)
    {
        CustomerId = customerId;
        BatchId = batchId;
        AssetFilter = assetFilter;
    }
}

public class GetBatchAssetsHandler : GetBatchAssetsBase<GetBatchAssets>
{
    public GetBatchAssetsHandler(DlcsContext dlcsContext) : base(dlcsContext)
    {
    }
    
    protected override IQueryable<Asset> GetBatchAssets(DlcsContext dlcsContext, GetBatchAssets request)
        => dlcsContext.Images
            .AsNoTracking()
            .IncludeDeliveryChannelsWithPolicy()
            .Where(a => a.Customer == request.CustomerId)
            .Include(a => a.BatchAssets.Where(ba => ba.BatchId == request.BatchId)); 
}