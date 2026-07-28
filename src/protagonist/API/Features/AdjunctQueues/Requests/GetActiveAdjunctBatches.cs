using API.Infrastructure.Page;
using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.AdjunctQueues.Requests;

/// <summary>
/// Get a paged list of all Active (incomplete) adjunct batches for customer
/// </summary>
public class GetActiveAdjunctBatches(int customerId) : IRequest<FetchEntityResult<PageOf<AdjunctBatch>>>, IPagedRequest,
    IOrderableRequest
{
    public int CustomerId { get; } = customerId;
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? Field { get; set; }
    public bool Descending { get; set; }
}

public class GetActiveAdjunctBatchesHandler(DlcsContext dlcsContext)
    : IRequestHandler<GetActiveAdjunctBatches, FetchEntityResult<PageOf<AdjunctBatch>>>
{
    public async Task<FetchEntityResult<PageOf<AdjunctBatch>>> Handle(GetActiveAdjunctBatches request,
        CancellationToken cancellationToken)
    {
        var result = await dlcsContext.AdjunctBatches.AsNoTracking().CreatePagedResult(request,
            q => q.Where(b => b.Customer == request.CustomerId && b.Finished == null),
            batches => batches.AsOrderedAdjunctBatchQuery(request),
            cancellationToken: cancellationToken);

        return FetchEntityResult<PageOf<AdjunctBatch>>.Success(result);
    }
}
