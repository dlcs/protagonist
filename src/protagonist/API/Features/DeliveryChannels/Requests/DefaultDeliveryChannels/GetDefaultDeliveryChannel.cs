using API.Infrastructure.Requests;
using DLCS.Model.DeliveryChannels;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DefaultDeliveryChannels;

public class GetDefaultDeliveryChannel : IRequest<FetchEntityResult<DefaultDeliveryChannel>>
{
    public int Customer { get; }
    
    public int Space { get; }
    
    public Guid DefaultDeliveryChannelId { get; }

    public GetDefaultDeliveryChannel(int customer, int space, Guid defaultDeliveryChannelId)
    {
        Customer = customer;
        Space = space;
        DefaultDeliveryChannelId = defaultDeliveryChannelId;
    }
}

public class GetDefaultDeliveryChannelHandler : IRequestHandler<GetDefaultDeliveryChannel,
    FetchEntityResult<DefaultDeliveryChannel>>
{
    private readonly DlcsContext dlcsContext;

    public GetDefaultDeliveryChannelHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<DefaultDeliveryChannel>> Handle(GetDefaultDeliveryChannel request,
        CancellationToken cancellationToken)
    {
        var defaultDeliveryChannel = await dlcsContext.DefaultDeliveryChannels.AsNoTracking()
            .Include(d => d.DeliveryChannelPolicy)
            .SingleOrDefaultAsync(d => 
                d.Id == request.DefaultDeliveryChannelId && 
                d.Customer == request.Customer &&
                d.Space == request.Space,
                cancellationToken);

        return defaultDeliveryChannel == null
            ? FetchEntityResult<DefaultDeliveryChannel>.NotFound()
            : FetchEntityResult<DefaultDeliveryChannel>.Success(defaultDeliveryChannel);
    }
}
