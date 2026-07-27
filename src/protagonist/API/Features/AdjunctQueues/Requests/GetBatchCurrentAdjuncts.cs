using DLCS.Model.Assets;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;

namespace API.Features.AdjunctQueues.Requests;

/// <summary>
/// Get details of adjuncts currently in batch, defined as adjuncts that have this batch as their Batch property
/// at time of request.
/// </summary>
/// <remarks>
/// See <see cref="GetBatchAdjuncts"/> for the historical equivalent, which returns adjuncts that were part of the
/// batch when it was created, regardless of whether they have since been reassigned to another batch
/// </remarks>
public class GetBatchCurrentAdjuncts(int customerId, int batchId) : GetBatchAdjuncts(customerId, batchId);

public class GetBatchCurrentAdjunctsHandler(DlcsContext dlcsContext)
    : GetBatchAdjunctsBase<GetBatchCurrentAdjuncts>(dlcsContext)
{
    protected override IQueryable<Adjunct> GetEntities(DlcsContext dlcsContext, GetBatchCurrentAdjuncts request)
        => dlcsContext.Adjuncts
            .AsNoTracking()
            .Where(a => a.Asset.Customer == request.CustomerId && a.Batch == request.BatchId);
}
