using API.Infrastructure.Requests;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannelPolicies.Requests;

public class GetDeliveryChannelPolicy: IRequest<FetchEntityResult<DeliveryChannelPolicy>>
{
    public int CustomerId { get; }
    public string ChannelName { get; set; }
    public string PolicyName { get; set; }
    
    public GetDeliveryChannelPolicy(int customerId, string channelName, string policyName)
    {
        CustomerId = customerId;
        ChannelName = channelName;
        PolicyName = policyName;
    }
}

public class GetDeliveryChannelPolicyHandler : IRequestHandler<GetDeliveryChannelPolicy, FetchEntityResult<DeliveryChannelPolicy>>
{
    private readonly DlcsContext dbContext;
    
    public GetDeliveryChannelPolicyHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<FetchEntityResult<DeliveryChannelPolicy>> Handle(GetDeliveryChannelPolicy request, CancellationToken cancellationToken)
    {
        var deliveryChannelPolicy = await dbContext.DeliveryChannelPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(p => 
                p.Customer == request.CustomerId && 
                p.Channel == request.ChannelName &&
                p.Name == request.PolicyName,
                cancellationToken);
        
        return deliveryChannelPolicy == null 
            ? FetchEntityResult<DeliveryChannelPolicy>.NotFound() 
            : FetchEntityResult<DeliveryChannelPolicy>.Success(deliveryChannelPolicy);
    }
}