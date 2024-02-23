using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests;

public class GetPoliciesFromDeliveryChannel: IRequest<FetchEntityResult<IReadOnlyCollection<DeliveryChannelPolicy>>>
{
    public int CustomerId { get; }
    public string DeliveryChannelName { get; }
    
    public GetPoliciesFromDeliveryChannel(int customerId, string deliveryChannelName)
    {
        CustomerId = customerId;
        DeliveryChannelName = deliveryChannelName;
    }
}

public class GetDeliveryChannelPoliciesHandler : IRequestHandler<GetPoliciesFromDeliveryChannel, FetchEntityResult<IReadOnlyCollection<DeliveryChannelPolicy>>>
{
    private readonly DlcsContext dbContext;
    
    public GetDeliveryChannelPoliciesHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<FetchEntityResult<IReadOnlyCollection<DeliveryChannelPolicy>>> Handle(GetPoliciesFromDeliveryChannel request, CancellationToken cancellationToken)
    {
        var deliveryChannelPolicies = await dbContext.DeliveryChannelPolicies
            .AsNoTracking()
            .Where(p =>
                p.Customer == request.CustomerId &&
                p.Channel == request.DeliveryChannelName)
            .ToListAsync(cancellationToken);

        return FetchEntityResult<IReadOnlyCollection<DeliveryChannelPolicy>>.Success(deliveryChannelPolicies);
    }
}