using API.Infrastructure.Page;
using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.AdjunctQueues.Requests;

/// <summary>
/// Get details of adjuncts within batch. This uses AdjunctBatchAdjuncts table to get historical data - i.e. all
/// adjuncts that were part of the batch at creation time, as long as they still exist
/// </summary>
public class GetBatchAdjuncts(int customerId, int batchId)
    : IRequest<FetchEntityResult<PageOf<Adjunct>>>, IPagedRequest, IOrderableRequest
{
    public int CustomerId { get; } = customerId;

    public int BatchId { get; } = batchId;

    public int Page { get; set; }

    public int PageSize { get; set; }

    public string? Field { get; set; }

    public bool Descending { get; set; }
}

public class GetBatchAdjunctsHandler(DlcsContext dlcsContext) : GetBatchAdjunctsBase<GetBatchAdjuncts>(dlcsContext)
{
    protected override IQueryable<Adjunct> GetEntities(DlcsContext dlcsContext, GetBatchAdjuncts request)
        => dlcsContext.AdjunctBatchAdjuncts
            .AsNoTracking()
            .Where(aba => aba.BatchId == request.BatchId && aba.Batch.Customer == request.CustomerId)
            .Select(aba => aba.Adjunct!);
}
