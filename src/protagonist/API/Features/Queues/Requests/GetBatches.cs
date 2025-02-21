using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Model.Page;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Queues.Requests;

/// <summary>
/// Get a paged list of all Active (incomplete and not superseded) batches for customer
/// </summary>
public class GetBatches : IRequest<FetchEntityResult<PageOf<Batch>>>, IPagedRequest
{
    public int CustomerId { get; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    
    public GetBatches(int customerId)
    {
        CustomerId = customerId;
    }
}

public class GetBatchesHandler : IRequestHandler<GetBatches, FetchEntityResult<PageOf<Batch>>>
{
    private readonly DlcsContext dlcsContext;

    public GetBatchesHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<PageOf<Batch>>> Handle(GetBatches request, CancellationToken cancellationToken)
    {
        var result = await dlcsContext.Batches.AsNoTracking().CreatePagedResult(request, 
            q => q.Where(b => b.Customer == request.CustomerId),
            batches => batches.OrderBy(b => b.Id),
            cancellationToken: cancellationToken);
        
        return FetchEntityResult<PageOf<Batch>>.Success(result);
    }
}