using API.Features.Assets.Query;
using API.Infrastructure.Page;
using DLCS.Model.Assets;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Queues.Requests;

public abstract class GetBatchAssetsBase<T>(DlcsContext dlcsContext)
    : GetBatchEntitiesBase<Asset, T>(dlcsContext)
    where T : GetBatchAssets
{
    protected override IQueryable<Asset> ApplyFilter(IQueryable<Asset> query, T request) =>
        query.ApplyAssetFilter(request.AssetQueryModel.Filter, true).AsSingleQuery();

    protected override IQueryable<Asset> ApplyOrdering(IQueryable<Asset> query, T request) =>
        query.AsOrderedAssetQuery(request);

    protected override Task<bool> DoesBatchExist(DlcsContext dlcsContext, T request,
        CancellationToken cancellationToken)
        => dlcsContext.Batches.AsNoTracking()
            .AnyAsync(b => b.Customer == request.CustomerId && b.Id == request.BatchId, cancellationToken);
}
