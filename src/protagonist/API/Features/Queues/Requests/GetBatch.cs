using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Queues.Requests;

/// <summary>
/// Get details of specified batch
/// </summary>
public class GetBatch : IRequest<FetchEntityResult<Batch>>
{
    public int CustomerId { get; }
    public int BatchId { get; }
    
    public GetBatch(int customerId, int batchId)
    {
        CustomerId = customerId;
        BatchId = batchId;
    }
}

public class GetBatchHandler : IRequestHandler<GetBatch, FetchEntityResult<Batch>>
{
    private readonly DlcsContext dlcsContext;
    private readonly ILogger<GetBatchHandler> logger;

    public GetBatchHandler(
        DlcsContext dlcsContext,
        ILogger<GetBatchHandler> logger)
    {
        this.dlcsContext = dlcsContext;
        this.logger = logger;
    }
    
    public async Task<FetchEntityResult<Batch>> Handle(GetBatch request, CancellationToken cancellationToken)
    {
        try
        {
            var batch = await dlcsContext.Batches.AsNoTracking()
                .SingleOrDefaultAsync(b => b.Customer == request.CustomerId && b.Id == request.BatchId,
                    cancellationToken);
            return batch == null
                ? FetchEntityResult<Batch>.NotFound()
                : FetchEntityResult<Batch>.Success(batch);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching batch {BatchId} for customer {CustomerId}", request.BatchId,
                request.CustomerId);
            return FetchEntityResult<Batch>.Failure("Unexplained error loading batch");
        }
    }
}