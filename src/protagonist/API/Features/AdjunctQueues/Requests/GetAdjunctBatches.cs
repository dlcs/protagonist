using API.Infrastructure.Page;
using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.AdjunctQueues.Requests;

/// <summary>
/// Get a paged list of all adjunct batches for customer, most recently submitted first by default
/// </summary>
public class GetAdjunctBatches(int customerId) : IRequest<FetchEntityResult<PageOf<AdjunctBatch>>>, IPagedRequest,
    IOrderableRequest
{
    public int CustomerId { get; } = customerId;
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? Field { get; set; }
    public bool Descending { get; set; } = true;
}

public class GetAdjunctBatchesHandler(DlcsContext dlcsContext)
    : IRequestHandler<GetAdjunctBatches, FetchEntityResult<PageOf<AdjunctBatch>>>
{
    public async Task<FetchEntityResult<PageOf<AdjunctBatch>>> Handle(GetAdjunctBatches request,
        CancellationToken cancellationToken)
    {
        var result = await dlcsContext.AdjunctBatches.AsNoTracking().CreatePagedResult(request,
            q => q.Where(b => b.Customer == request.CustomerId),
            batches => batches.AsOrderedAdjunctBatchQuery(request),
            cancellationToken: cancellationToken);

        return FetchEntityResult<PageOf<AdjunctBatch>>.Success(result);
    }
}
