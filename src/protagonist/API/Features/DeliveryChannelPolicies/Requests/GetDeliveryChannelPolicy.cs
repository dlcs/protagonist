using API.Infrastructure.Requests;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannelPolicies.Requests;

public class GetDeliveryChannelPolicy: IRequest<FetchEntityResult<DeliveryChannelPolicy>>
{
    public int CustomerId { get; }
    public string DeliveryChannelName { get; set; }
    public string DeliveryChannelPolicyName { get; set; }
    
    public GetDeliveryChannelPolicy(int customerId, string deliveryChannelName, string deliveryChannelPolicyName)
    {
        CustomerId = customerId;
        DeliveryChannelName = deliveryChannelName;
        DeliveryChannelPolicyName = deliveryChannelPolicyName;
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
                p.Channel == request.DeliveryChannelName &&
                p.Name == request.DeliveryChannelPolicyName,
                cancellationToken);
        
        return deliveryChannelPolicy == null 
            ? FetchEntityResult<DeliveryChannelPolicy>.NotFound() 
            : FetchEntityResult<DeliveryChannelPolicy>.Success(deliveryChannelPolicy);
    }
}