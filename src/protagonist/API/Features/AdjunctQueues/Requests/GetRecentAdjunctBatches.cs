using API.Infrastructure.Page;
using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.AdjunctQueues.Requests;

/// <summary>
/// Get a paged list of all Recent (finished, ordered by finished DESC) adjunct batches for customer
/// </summary>
public class GetRecentAdjunctBatches(int customerId) : IRequest<FetchEntityResult<PageOf<AdjunctBatch>>>, IPagedRequest
{
    public int CustomerId { get; } = customerId;
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class GetRecentAdjunctBatchesHandler(DlcsContext dlcsContext)
    : IRequestHandler<GetRecentAdjunctBatches, FetchEntityResult<PageOf<AdjunctBatch>>>
{
    public async Task<FetchEntityResult<PageOf<AdjunctBatch>>> Handle(GetRecentAdjunctBatches request,
        CancellationToken cancellationToken)
    {
        var result = await dlcsContext.AdjunctBatches.AsNoTracking().CreatePagedResult(
            request,
            q => q.Where(b => b.Customer == request.CustomerId && b.Finished != null),
            batches => batches.OrderByDescending(b => b.Finished),
            cancellationToken: cancellationToken);

        return FetchEntityResult<PageOf<AdjunctBatch>>.Success(result);
    }
}
