using System.Threading;
using System.Threading.Tasks;
using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Model.Page;
using DLCS.Repository;
using MediatR;

namespace API.Features.Queues.Requests;

/// <summary>
/// Get a paged list of all Active (incomplete and not superseded) batches for customer
/// </summary>
public class GetActiveBatches : IRequest<FetchEntityResult<PageOf<Batch>>>, IPagedRequest
{
    public int CustomerId { get; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    
    public GetActiveBatches(int customerId)
    {
        CustomerId = customerId;
    }
}

public class GetActiveBatchesHandler : IRequestHandler<GetActiveBatches, FetchEntityResult<PageOf<Batch>>>
{
    private readonly DlcsContext dlcsContext;

    public GetActiveBatchesHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<PageOf<Batch>>> Handle(GetActiveBatches request, CancellationToken cancellationToken)
    {
        var result = await dlcsContext.Batches.CreatePagedResult(
            b => b.Customer == request.CustomerId && b.Finished == null && !b.Superseded,
            request, 
            cancellationToken);
        
        return FetchEntityResult<PageOf<Batch>>.Success(result);
    }
}