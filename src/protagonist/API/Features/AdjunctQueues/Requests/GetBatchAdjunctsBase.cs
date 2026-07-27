using API.Infrastructure.Page;
using DLCS.Model.Assets;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;

namespace API.Features.AdjunctQueues.Requests;

public abstract class GetBatchAdjunctsBase<T>(DlcsContext dlcsContext)
    : GetBatchEntitiesBase<Adjunct, T>(dlcsContext)
    where T : GetBatchAdjuncts
{
    protected override IQueryable<Adjunct> ApplyOrdering(IQueryable<Adjunct> query, T request) =>
        query.AsOrderedAdjunctQuery(request);

    protected override Task<bool> DoesBatchExist(DlcsContext dlcsContext, T request,
        CancellationToken cancellationToken)
        => dlcsContext.AdjunctBatches.AsNoTracking()
            .AnyAsync(b => b.Customer == request.CustomerId && b.Id == request.BatchId, cancellationToken);
}
