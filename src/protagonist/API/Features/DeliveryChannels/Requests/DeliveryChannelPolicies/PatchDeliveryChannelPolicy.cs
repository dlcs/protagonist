using API.Features.DeliveryChannels.Helpers;
using API.Infrastructure.Requests;
using API.Infrastructure.Requests.Pipelines;
using DLCS.Core;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;

namespace API.Features.DeliveryChannels.Requests.DeliveryChannelPolicies;

/// <summary>
/// Partial update of DeliveryChannelPolicy, only DisplayName and PolicyData can be updated
/// </summary>
public class PatchDeliveryChannelPolicy : IRequest<ModifyEntityResult<DeliveryChannelPolicy>>, IInvalidateCaches
{
    public int CustomerId { get; }

    public string Channel { get; }
    
    public string PolicyName { get; }
    
    public string? DisplayName { get; }

    public string? PolicyData { get; }
    
    public PatchDeliveryChannelPolicy(int customerId, string channel, string policyName, string? displayName, string? policyData)
    {
        CustomerId = customerId;
        Channel = channel;
        PolicyName = policyName;
        DisplayName = displayName;
        PolicyData = policyData;
    }
    
    public string[] InvalidatedCacheKeys => new[]
        { CacheKeys.DeliveryChannelPolicies(CustomerId), CacheKeys.DefaultDeliveryChannels(CustomerId) };
}

public class PatchDeliveryChannelPolicyHandler : IRequestHandler<PatchDeliveryChannelPolicy, ModifyEntityResult<DeliveryChannelPolicy>>
{
    private readonly DlcsContext dbContext;
    
    public PatchDeliveryChannelPolicyHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ModifyEntityResult<DeliveryChannelPolicy>> Handle(PatchDeliveryChannelPolicy request,
        CancellationToken cancellationToken)
    {
        var existingDeliveryChannelPolicy =
            await dbContext.DeliveryChannelPolicies.GetDeliveryChannel(request.CustomerId, request.Channel,
                request.PolicyName, cancellationToken);

        if (existingDeliveryChannelPolicy == null)
        {
            return ModifyEntityResult<DeliveryChannelPolicy>.Failure(
                $"A policy for delivery channel '{request.Channel}' called '{request.PolicyName}' was not found" , 
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