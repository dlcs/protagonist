using API.Features.DeliveryChannels.Helpers;
using API.Infrastructure.Requests;
using API.Infrastructure.Requests.Pipelines;
using DLCS.Core;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DeliveryChannelPolicies;

/// <summary>
/// Delete DeliveryChannelPolicy with specified name for channel
/// </summary>
public class DeleteDeliveryChannelPolicy: IRequest<DeleteEntityResult>, IInvalidateCaches
{
    public int CustomerId { get; }
    public string DeliveryChannelName { get; }
    public string DeliveryChannelPolicyName { get; }
    
    public DeleteDeliveryChannelPolicy(int customerId, string deliveryChannelName, string deliveryChannelPolicyName)
    {
        CustomerId = customerId;
        DeliveryChannelName = deliveryChannelName;
        DeliveryChannelPolicyName = deliveryChannelPolicyName;
    }
    
    public string[] InvalidatedCacheKeys => new[]
        { CacheKeys.DeliveryChannelPolicies(CustomerId), CacheKeys.DefaultDeliveryChannels(CustomerId) };
}

public class DeleteDeliveryChannelPolicyHandler : IRequestHandler<DeleteDeliveryChannelPolicy, DeleteEntityResult>
{
    private readonly DlcsContext dbContext;
    
    public DeleteDeliveryChannelPolicyHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<DeleteEntityResult> Handle(DeleteDeliveryChannelPolicy request, CancellationToken cancellationToken)
    {
        var policy =
            await dbContext.DeliveryChannelPolicies.GetDeliveryChannel(request.CustomerId, request.DeliveryChannelName,
                request.DeliveryChannelPolicyName, cancellationToken);
        
        if (policy == null)
        {
            return DeleteEntityResult.Failure(
                $"Deletion failed - Delivery channel policy ${request.DeliveryChannelPolicyName} was not found", DeleteResult.NotFound);
        }
        
        var policyInUseByDefaultDeliveryChannel = await dbContext.DefaultDeliveryChannels.AnyAsync(c =>
            c.DeliveryChannelPolicyId == policy.Id,
            cancellationToken);

        var policyInUseByAsset = await dbContext.ImageDeliveryChannels.AnyAsync(c =>
            c.DeliveryChannelPolicyId == policy.Id,
            cancellationToken);
        
        if (policyInUseByDefaultDeliveryChannel || policyInUseByAsset)
        {
            return DeleteEntityResult.Failure(
                $"Deletion failed - Delivery channel policy {request.DeliveryChannelPolicyName} is still in use", DeleteResult.Conflict);
        }
        
        dbContext.DeliveryChannelPolicies.Remove(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return DeleteEntityResult.Success;
    }
}