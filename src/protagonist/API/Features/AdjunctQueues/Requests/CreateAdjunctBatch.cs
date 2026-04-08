using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using MediatR;

namespace API.Features.AdjunctQueues.Requests;

public class CreateAdjunctBatch(int customerId, Adjunct[] adjuncts) : IRequest<ModifyEntityResult<AdjunctBatch>>
{
    public int CustomerId { get; } = customerId;
    public Adjunct[] Adjuncts { get; } = adjuncts;
}

public class CreateAdjunctBatchHandler : IRequestHandler<CreateAdjunctBatch, ModifyEntityResult<AdjunctBatch>>
{
    public Task<ModifyEntityResult<AdjunctBatch>> Handle(CreateAdjunctBatch request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
