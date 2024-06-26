﻿using API.Features.DeliveryChannels.Helpers;
using API.Infrastructure.Requests;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DeliveryChannelPolicies;

public class GetDeliveryChannelPolicy: IRequest<FetchEntityResult<DeliveryChannelPolicy>>
{
    public int CustomerId { get; }
    public string DeliveryChannelName { get; }
    public string DeliveryChannelPolicyName { get; }
    
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
            .GetDeliveryChannel(request.CustomerId, request.DeliveryChannelName, request.DeliveryChannelPolicyName,
                cancellationToken);
        
        return deliveryChannelPolicy == null 
            ? FetchEntityResult<DeliveryChannelPolicy>.NotFound() 
            : FetchEntityResult<DeliveryChannelPolicy>.Success(deliveryChannelPolicy);
    }
}