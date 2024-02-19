using API.Infrastructure.Requests;
using DLCS.Model.DeliveryChannels;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests;

public class GetCustomerDefaultDeliveryChannel : IRequest<FetchEntityResult<DefaultDeliveryChannel>>
{
    public string DefaultDeliveryChannelId { get; }
    
    public GetCustomerDefaultDeliveryChannel(string defaultDeliveryChannelId)
    {
        DefaultDeliveryChannelId = defaultDeliveryChannelId;
    }
}

public class GetCustomerDefaultDeliveryChannelHandler : IRequestHandler<GetCustomerDefaultDeliveryChannel,
    FetchEntityResult<DefaultDeliveryChannel>>
{
    private readonly DlcsContext dlcsContext;

    public GetCustomerDefaultDeliveryChannelHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<DefaultDeliveryChannel>> Handle(GetCustomerDefaultDeliveryChannel request,
        CancellationToken cancellationToken)
    {
        var isGuid = Guid.TryParse(request.DefaultDeliveryChannelId, out var defaultDeliveryChannelGuid);

        if (!isGuid) return FetchEntityResult<DefaultDeliveryChannel>.Failure("Could not parse id");


        var defaultDeliveryChannel = await dlcsContext.DefaultDeliveryChannels.AsNoTracking()
            .Include(d => d.DeliveryChannelPolicy)
            .SingleOrDefaultAsync(b => b.Id == defaultDeliveryChannelGuid,
                cancellationToken);

        return defaultDeliveryChannel == null
            ? FetchEntityResult<DefaultDeliveryChannel>.NotFound()
            : FetchEntityResult<DefaultDeliveryChannel>.Success(defaultDeliveryChannel);
    }
}
