using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets;
using MediatR;

namespace API.Features.Queues.Requests;

/// <summary>
/// Handler that creates an empty batch of 0 images
/// </summary>
public class CreateEmptyBatch : IRequest<ModifyEntityResult<Batch>>
{
    public int CustomerId { get; }
    
    public CreateEmptyBatch(int customerId)
    {
        CustomerId = customerId;
    }
}

public class CreateEmptyBatchHandler : IRequestHandler<CreateEmptyBatch, ModifyEntityResult<Batch>>
{
    private readonly IBatchRepository batchRepository;

    public CreateEmptyBatchHandler(IBatchRepository batchRepository)
    {
        this.batchRepository = batchRepository;
    }
    
    public async Task<ModifyEntityResult<Batch>> Handle(CreateEmptyBatch request, CancellationToken cancellationToken)
    {
        var batch = await batchRepository.CreateBatch(request.CustomerId, Array.Empty<Asset>(), cancellationToken);
        return ModifyEntityResult<Batch>.Success(batch, WriteResult.Created);
    }
}