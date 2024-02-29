using API.Features.DeliveryChannels.Validation;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DeliveryChannelPolicies;

public class PatchDeliveryChannelPolicy : IRequest<ModifyEntityResult<DeliveryChannelPolicy>>
{
    public int CustomerId { get; }

    public string Channel { get; }
    
    public string Name { get; }
    
    public string? DisplayName { get; }

    public string? PolicyData { get; }
    
    public PatchDeliveryChannelPolicy(int customerId, string channel, string name, string? displayName, string? policyData)
    {
        CustomerId = customerId;
        Channel = channel;
        Name = name;
        DisplayName = displayName;
        PolicyData = policyData;
    }
}

public class PatchDeliveryChannelPolicyHandler : IRequestHandler<PatchDeliveryChannelPolicy, ModifyEntityResult<DeliveryChannelPolicy>>
{
    private readonly DlcsContext dbContext;
    
    public PatchDeliveryChannelPolicyHandler(DlcsContext dbContext, DeliveryChannelPolicyDataValidator policyDataValidator)
    {
        this.dbContext = dbContext;
    }

    public async Task<ModifyEntityResult<DeliveryChannelPolicy>> Handle(PatchDeliveryChannelPolicy request,
        CancellationToken cancellationToken)
    {
        var existingDeliveryChannelPolicy = await dbContext.DeliveryChannelPolicies.SingleOrDefaultAsync(p =>
            p.Customer == request.CustomerId &&
            p.Channel == request.Channel &&
            p.Name == request.Name,
            cancellationToken);

        if (existingDeliveryChannelPolicy == null)
        {
            return ModifyEntityResult<DeliveryChannelPolicy>.Failure(
                $"A policy for delivery channel '{request.Channel}' called '{request.Name}' was not found" , 
                WriteResult.NotFound);
        }
        
        var hasBeenChanged = false;
            
        if (request.DisplayName != null)
        {
            existingDeliveryChannelPolicy.DisplayName = request.DisplayName;
            hasBeenChanged = true;
        }
            
        if (request.PolicyData != null) 
        {
            existingDeliveryChannelPolicy.PolicyData = request.PolicyData;
            hasBeenChanged = true;
        }
            
        if (hasBeenChanged)
        {
            existingDeliveryChannelPolicy.Modified = DateTime.UtcNow;
            var rowCount = await dbContext.SaveChangesAsync(cancellationToken);
            if (rowCount == 0)
            {
                return ModifyEntityResult<DeliveryChannelPolicy>.Failure("Unable to patch delivery channel policy", WriteResult.Error);
            }
        }
        
        return ModifyEntityResult<DeliveryChannelPolicy>.Success(existingDeliveryChannelPolicy);
    }
}