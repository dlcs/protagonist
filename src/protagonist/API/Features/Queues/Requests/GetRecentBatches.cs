using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Model.Page;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Queues.Requests;

/// <summary>
/// Get a paged list of all Recent (finished, order by finished DESC) batches for customer
/// </summary>
public class GetRecentBatches : IRequest<FetchEntityResult<PageOf<Batch>>>, IPagedRequest
{
    public int CustomerId { get; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    
    public GetRecentBatches(int customerId)
    {
        CustomerId = customerId;
    }
}

public class GetRecentBatchesHandler : IRequestHandler<GetRecentBatches, FetchEntityResult<PageOf<Batch>>>
{
    private readonly DlcsContext dlcsContext;

    public GetRecentBatchesHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<PageOf<Batch>>> Handle(GetRecentBatches request, CancellationToken cancellationToken)
    {
        var result = await dlcsContext.Batches.AsNoTracking().CreatePagedResult(
            request,
            b => b.Customer == request.CustomerId && b.Finished != null,
            batches => batches.OrderByDescending(b => b.Finished), 
            cancellationToken: cancellationToken);
        
        return FetchEntityResult<PageOf<Batch>>.Success(result);
    }
}