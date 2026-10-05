using API.Infrastructure.Requests;
using DLCS.Model.Processing;
using MediatR;

namespace API.Features.AdjunctQueues.Requests;

/// <summary>
/// Get details of customer adjunct queue
/// </summary>
public class GetCustomerAdjunctQueue(int customerId) : IRequest<FetchEntityResult<AdjunctQueue>>
{
    public int CustomerId { get; } = customerId;
}

public class GetCustomerAdjunctQueueHandler(ICustomerQueueRepository customerQueueRepository)
    : IRequestHandler<GetCustomerAdjunctQueue, FetchEntityResult<AdjunctQueue>>
{
    public async Task<FetchEntityResult<AdjunctQueue>> Handle(GetCustomerAdjunctQueue request,
        CancellationToken cancellationToken)
    {
        var adjunctQueue = await customerQueueRepository.GetAdjunctQueue(request.CustomerId, cancellationToken);

        return adjunctQueue == null
            ? FetchEntityResult<AdjunctQueue>.NotFound()
            : FetchEntityResult<AdjunctQueue>.Success(adjunctQueue);
    }
}
