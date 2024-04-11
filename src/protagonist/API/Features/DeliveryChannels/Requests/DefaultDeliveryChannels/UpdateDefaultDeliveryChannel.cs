using API.Features.DeliveryChannels.Helpers;
using API.Infrastructure.Requests;
using API.Infrastructure.Requests.Pipelines;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Model.DeliveryChannels;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DefaultDeliveryChannels;

/// <summary>
/// Update default delivery channel - can update Space, MediaType and associated Policy
/// </summary>
public class UpdateDefaultDeliveryChannel : IRequest<ModifyEntityResult<DefaultDeliveryChannel>>, IInvalidateCaches
{
    public int Customer { get; }

    public int Space { get; }
    
    public string Policy { get; }
    
    public string Channel { get; }
    
    public string MediaType { get; }
    
    public Guid Id { get; }

    public UpdateDefaultDeliveryChannel(
        int customerId, 
        int space, 
        string policy, 
        string channel, 
        string mediaType,
        Guid id)
    {
        Customer = customerId;
        MediaType = mediaType;
        Space = space;
        Policy = policy;
        Channel = channel;
        Id = id;
        Space = space;
    }

    public string[] InvalidatedCacheKeys => CacheKeys.DefaultDeliveryChannels(Customer).AsArray();
}

public class UpdateDefaultDeliveryChannelHandler : IRequestHandler<UpdateDefaultDeliveryChannel, ModifyEntityResult<DefaultDeliveryChannel>>
{  
    private readonly DlcsContext dbContext;

    public UpdateDefaultDeliveryChannelHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<ModifyEntityResult<DefaultDeliveryChannel>> Handle(UpdateDefaultDeliveryChannel request, CancellationToken cancellationToken)
    {
        var defaultDeliveryChannel = await dbContext.DefaultDeliveryChannels.SingleOrDefaultAsync(
            d => d.Customer == request.Customer && d.Id == request.Id, cancellationToken);
        
        if (defaultDeliveryChannel == null)
        {
            return ModifyEntityResult<DefaultDeliveryChannel>.Failure($"Couldn't find a default delivery channel with the id {request.Id}",
                WriteResult.NotFound);
        }

        defaultDeliveryChannel.MediaType = request.MediaType;
        defaultDeliveryChannel.Space = request.Space;
        
        try
        {
            var deliveryChannelPolicy = dbContext.DeliveryChannelPolicies.RetrieveDeliveryChannel(
                request.Customer, 
                request.Channel,
                request.Policy);

            defaultDeliveryChannel.DeliveryChannelPolicyId = deliveryChannelPolicy.Id;
        }
        catch (InvalidOperationException)
        {
            return ModifyEntityResult<DefaultDeliveryChannel>.Failure("Failed to find linked delivery channel policy", WriteResult.BadRequest);
        }

        await dbContext.SaveChangesAsync(cancellationToken); 

        return ModifyEntityResult<DefaultDeliveryChannel>.Success(defaultDeliveryChannel);
    }
}