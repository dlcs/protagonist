using API.Features.DeliveryChannels.Helpers;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.DeliveryChannels;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DefaultDeliveryChannels;

public class UpdateDefaultDeliveryChannel : IRequest<ModifyEntityResult<UpdateDefaultDeliveryChannelResult>>
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
}

public class UpdateDefaultDeliveryChannelResult
{
    public DefaultDeliveryChannel? DefaultDeliveryChannel { get; init; }
}

public class UpdateDefaultDeliveryChannelHandler : IRequestHandler<UpdateDefaultDeliveryChannel, ModifyEntityResult<UpdateDefaultDeliveryChannelResult>>
{  
    private readonly DlcsContext dbContext;
    private readonly IDeliveryChannelPolicyRepository deliveryChannelPolicyRepository;

    public UpdateDefaultDeliveryChannelHandler(DlcsContext dbContext, 
        IDeliveryChannelPolicyRepository deliveryChannelPolicyRepository)
    {
        this.dbContext = dbContext;
        this.deliveryChannelPolicyRepository = deliveryChannelPolicyRepository;
    }
    
    public async Task<ModifyEntityResult<UpdateDefaultDeliveryChannelResult>> Handle(UpdateDefaultDeliveryChannel request, CancellationToken cancellationToken)
    {
        var defaultDeliveryChannel = await dbContext.DefaultDeliveryChannels.SingleOrDefaultAsync(
            d => d.Customer == request.Customer && d.Id == request.Id, cancellationToken);
        
        if (defaultDeliveryChannel == null)
        {
            return ModifyEntityResult<UpdateDefaultDeliveryChannelResult>.Failure($"Couldn't find a default delivery channel with the id {request.Id}",
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
            return ModifyEntityResult<UpdateDefaultDeliveryChannelResult>.Failure("Failed to find linked delivery channel policy", WriteResult.BadRequest);
        }
        

        var updatedDefaultDeliveryChannel =  dbContext.DefaultDeliveryChannels.Update(defaultDeliveryChannel);

        await dbContext.SaveChangesAsync(cancellationToken); 
        
        var updated = new UpdateDefaultDeliveryChannelResult()
        {
            DefaultDeliveryChannel = updatedDefaultDeliveryChannel.Entity
        };

        return ModifyEntityResult<UpdateDefaultDeliveryChannelResult>.Success(updated);
    }
}