using DLCS.Model.Assets;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;

namespace API.Features.AdjunctQueues.Requests;

/// <summary>
/// Get details of adjuncts within batch. This will include adjuncts currently in that batch only
/// </summary>
/// <remarks>
/// Although the behaviour is slightly different, this has been superseded by <see cref="GetBatchAdjuncts"/>, which
/// returns historical data as well as current batch data
/// </remarks>
public class GetBatchCurrentAdjuncts(int customerId, int batchId) : GetBatchAdjuncts(customerId, batchId);

public class GetBatchCurrentAdjunctsHandler(DlcsContext dlcsContext)
    : GetBatchAdjunctsBase<GetBatchCurrentAdjuncts>(dlcsContext)
{
    protected override IQueryable<Adjunct> GetAdjuncts(DlcsContext dlcsContext, GetBatchCurrentAdjuncts request)
        => dlcsContext.Adjuncts
            .AsNoTracking()
            .Where(a => a.Asset.Customer == request.CustomerId && a.Batch == request.BatchId);
}
