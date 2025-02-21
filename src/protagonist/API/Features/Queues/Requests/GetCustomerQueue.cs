using API.Infrastructure.Requests;
using DLCS.Model.Processing;
using MediatR;

namespace API.Features.Queues.Requests;

/// <summary>
/// Get details of customer queue
/// </summary>
public class GetCustomerQueue : IRequest<FetchEntityResult<CustomerQueue>>
{
    public int CustomerId { get; }
    
    public string Name { get; }

    public GetCustomerQueue(int customerId, string name = "default")
    {
        CustomerId = customerId;
        Name = name;
    }
}

public class GetQueueHandler : IRequestHandler<GetCustomerQueue, FetchEntityResult<CustomerQueue>>
{
    private readonly ICustomerQueueRepository customerQueueRepository;

    public GetQueueHandler(ICustomerQueueRepository customerQueueRepository)
    {
        this.customerQueueRepository = customerQueueRepository;
    }

    public async Task<FetchEntityResult<CustomerQueue>> Handle(GetCustomerQueue request,
        CancellationToken cancellationToken)
    {
        var customerQueue = await customerQueueRepository.Get(request.CustomerId, request.Name, cancellationToken);

        return customerQueue == null
            ? FetchEntityResult<CustomerQueue>.NotFound()
            : FetchEntityResult<CustomerQueue>.Success(customerQueue);
    }
}