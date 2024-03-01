using API.Features.DeliveryChannels.Helpers;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.DeliveryChannels;
using DLCS.Repository;
using DLCS.Repository.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DefaultDeliveryChannels;

public class CreateDefaultDeliveryChannel : IRequest<ModifyEntityResult<CreateDefaultDeliveryChannelResult>>
{
    public int Customer { get; }
    
    public int Space { get; }
    
    public string Policy { get; }
    
    public string Channel { get; }
    
    public string MediaType { get; }
    
    public CreateDefaultDeliveryChannel(int customer, int space, string policy, string channel, string mediaType)
    {
        Customer = customer;
        Policy = policy;
        Channel = channel;
        MediaType = mediaType;
        Space = space;
    }
}

public class CreateDefaultDeliveryChannelResult
{
    public CreateDefaultDeliveryChannelResult(DefaultDeliveryChannel defaultDeliveryChannel)
    {
        DefaultDeliveryChannel = defaultDeliveryChannel;
    }

    public DefaultDeliveryChannel DefaultDeliveryChannel { get; init; }
}

public class CreateDefaultDeliveryChannelHandler : IRequestHandler<CreateDefaultDeliveryChannel,
    ModifyEntityResult<CreateDefaultDeliveryChannelResult>>
{
    private readonly DlcsContext dbContext;
    private readonly IDeliveryChannelPolicyRepository deliveryChannelPolicyRepository;

    public CreateDefaultDeliveryChannelHandler(DlcsContext dbContext, 
        IDeliveryChannelPolicyRepository deliveryChannelPolicyRepository)
    {
        this.dbContext = dbContext;
        this.deliveryChannelPolicyRepository = deliveryChannelPolicyRepository;
    }

    public async Task<ModifyEntityResult<CreateDefaultDeliveryChannelResult>> Handle(
        CreateDefaultDeliveryChannel request, CancellationToken cancellationToken)
    {
        var defaultDeliveryChannel = new DefaultDeliveryChannel()
        {
            Customer = request.Customer,
            Space = request.Space,
            MediaType = request.MediaType
        };

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
            return ModifyEntityResult<CreateDefaultDeliveryChannelResult>.Failure("Failed to find linked delivery channel policy", WriteResult.BadRequest);
        }

        var returnedDefaultDeliveryChannel = dbContext.DefaultDeliveryChannels.Add(defaultDeliveryChannel);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.GetDatabaseError() is UniqueConstraintError)
        {
            return ModifyEntityResult<CreateDefaultDeliveryChannelResult>.Failure(
                $"A default delivery channel for the requested media type '{defaultDeliveryChannel.MediaType}' already exists",
                WriteResult.Conflict);
        }

        var created = new CreateDefaultDeliveryChannelResult(returnedDefaultDeliveryChannel.Entity);

        return ModifyEntityResult<CreateDefaultDeliveryChannelResult>.Success(created, WriteResult.Created);
    }
}