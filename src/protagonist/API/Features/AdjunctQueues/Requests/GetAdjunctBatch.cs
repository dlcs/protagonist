using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.AdjunctQueues.Requests;

/// <summary>
/// Get details of specified adjunct batch
/// </summary>
public class GetAdjunctBatch(int customerId, int batchId) : IRequest<FetchEntityResult<AdjunctBatch>>
{
    public int CustomerId { get; } = customerId;
    public int BatchId { get; } = batchId;
}

public class GetAdjunctBatchHandler(
    DlcsContext dlcsContext,
    ILogger<GetAdjunctBatchHandler> logger)
    : IRequestHandler<GetAdjunctBatch, FetchEntityResult<AdjunctBatch>>
{
    public async Task<FetchEntityResult<AdjunctBatch>> Handle(GetAdjunctBatch request,
        CancellationToken cancellationToken)
    {
        try
        {
            var batch = await dlcsContext.AdjunctBatches.AsNoTracking()
                .SingleOrDefaultAsync(b => b.Customer == request.CustomerId && b.Id == request.BatchId,
                    cancellationToken);
            return batch == null
                ? FetchEntityResult<AdjunctBatch>.NotFound()
                : FetchEntityResult<AdjunctBatch>.Success(batch);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching adjunct batch {BatchId} for customer {CustomerId}",
                request.BatchId, request.CustomerId);
            return FetchEntityResult<AdjunctBatch>.Failure("Unexplained error loading adjunct batch");
        }
    }
}
