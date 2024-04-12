using API.Features.DeliveryChannels.Helpers;
using API.Infrastructure.Requests;
using API.Infrastructure.Requests.Pipelines;
using DLCS.Core;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;

namespace API.Features.DeliveryChannels.Requests.DeliveryChannelPolicies;

/// <summary>
/// Create or update DeliveryChannelPolicy, only DisplayName and PolicyData can be updated
/// </summary>
public class UpsertDeliveryChannelPolicy : IRequest<ModifyEntityResult<DeliveryChannelPolicy>>, IInvalidateCaches
{
    public int CustomerId { get; }
    
    public DeliveryChannelPolicy DeliveryChannelPolicy { get; }
    
    public UpsertDeliveryChannelPolicy(int customerId, DeliveryChannelPolicy deliveryChannelPolicy)
    {
        CustomerId = customerId;
        DeliveryChannelPolicy = deliveryChannelPolicy;
    }
    
    public string[] InvalidatedCacheKeys => new[]
        { CacheKeys.DeliveryChannelPolicies(CustomerId), CacheKeys.DefaultDeliveryChannels(CustomerId) };
}

public class UpsertDeliveryChannelPolicyHandler : IRequestHandler<UpsertDeliveryChannelPolicy, ModifyEntityResult<DeliveryChannelPolicy>>
{
    private readonly DlcsContext dbContext;
    
    public UpsertDeliveryChannelPolicyHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<ModifyEntityResult<DeliveryChannelPolicy>> Handle(UpsertDeliveryChannelPolicy request, CancellationToken cancellationToken)
    {
        var existingDeliveryChannelPolicy = await dbContext.DeliveryChannelPolicies
            .GetDeliveryChannel(request.CustomerId,
                request.DeliveryChannelPolicy.Channel,
                request.DeliveryChannelPolicy.Name,
                cancellationToken);

        if (existingDeliveryChannelPolicy != null)
        {
            existingDeliveryChannelPolicy.DisplayName = request.DeliveryChannelPolicy.DisplayName;
            existingDeliveryChannelPolicy.Modified = DateTime.UtcNow;
            existingDeliveryChannelPolicy.PolicyData = request.DeliveryChannelPolicy.PolicyData;
            
            await dbContext.SaveChangesAsync(cancellationToken); 
    
            return ModifyEntityResult<DeliveryChannelPolicy>.Success(existingDeliveryChannelPolicy);
        }
        else
        {
            var newDeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Customer = request.CustomerId,
                Name = request.DeliveryChannelPolicy.Name,
                DisplayName = request.DeliveryChannelPolicy.DisplayName,
                Channel = request.DeliveryChannelPolicy.Channel,
                System = false,
                Modified = DateTime.UtcNow,
                Created = DateTime.UtcNow,
                PolicyData = request.DeliveryChannelPolicy.PolicyData,
            };
            
            await dbContext.DeliveryChannelPolicies.AddAsync(newDeliveryChannelPolicy, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken); 
            
            return ModifyEntityResult<DeliveryChannelPolicy>.Success(newDeliveryChannelPolicy, WriteResult.Created);
        }
    }
}