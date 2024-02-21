using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests;

public class DeleteDeliveryChannelPolicy: IRequest<ResultMessage<DeleteResult>>
{
    public int CustomerId { get; }
    public string DeliveryChannelName { get; set; }
    public string DeliveryChannelPolicyName { get; set; }
    
    public DeleteDeliveryChannelPolicy(int customerId, string deliveryChannelName, string deliveryChannelPolicyName)
    {
        CustomerId = customerId;
        DeliveryChannelName = deliveryChannelName;
        DeliveryChannelPolicyName = deliveryChannelPolicyName;
    }
}

public class DeleteDeliveryChannelPolicyHandler : IRequestHandler<DeleteDeliveryChannelPolicy, ResultMessage<DeleteResult>>
{
    private readonly DlcsContext dbContext;
    
    public DeleteDeliveryChannelPolicyHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<ResultMessage<DeleteResult>> Handle(DeleteDeliveryChannelPolicy request, CancellationToken cancellationToken)
    {
        var policy = await dbContext.DeliveryChannelPolicies.SingleOrDefaultAsync(p =>
            p.Name == request.DeliveryChannelPolicyName &&
            p.Channel == request.DeliveryChannelName,
            cancellationToken);
        
        if (policy == null)
        {
            return new ResultMessage<DeleteResult>(
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
            return new ResultMessage<DeleteResult>(
                $"Deletion failed - Delivery channel policy {request.DeliveryChannelPolicyName} is still in use", DeleteResult.Conflict);
        }
        
        dbContext.DeliveryChannelPolicies.Remove(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return new ResultMessage<DeleteResult>(
            $"Delivery channel policy {request.DeliveryChannelPolicyName} successfully deleted", DeleteResult.Deleted);
    }
}