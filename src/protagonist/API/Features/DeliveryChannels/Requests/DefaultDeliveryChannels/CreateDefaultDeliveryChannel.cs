using API.Features.DeliveryChannels.Helpers;
using API.Infrastructure.Requests;
using API.Infrastructure.Requests.Pipelines;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Model.DeliveryChannels;
using DLCS.Repository;
using DLCS.Repository.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DefaultDeliveryChannels;

/// <summary>
/// Create a new DefaultDeliveryChannel object in DB
/// </summary>
public class CreateDefaultDeliveryChannel : IRequest<ModifyEntityResult<DefaultDeliveryChannel>>, IInvalidateCaches
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

    public string[] InvalidatedCacheKeys => CacheKeys.DefaultDeliveryChannels(Customer).AsArray();
}

public class CreateDefaultDeliveryChannelHandler : IRequestHandler<CreateDefaultDeliveryChannel,
    ModifyEntityResult<DefaultDeliveryChannel>>
{
    private readonly DlcsContext dbContext;

    public CreateDefaultDeliveryChannelHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ModifyEntityResult<DefaultDeliveryChannel>> Handle(
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
            return ModifyEntityResult<DefaultDeliveryChannel>.Failure("Failed to find linked delivery channel policy", WriteResult.BadRequest);
        }

        dbContext.DefaultDeliveryChannels.Add(defaultDeliveryChannel);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.GetDatabaseError() is UniqueConstraintError)
        {
            return ModifyEntityResult<DefaultDeliveryChannel>.Failure(
                $"A default delivery channel for the requested media type '{defaultDeliveryChannel.MediaType}' already exists",
                WriteResult.Conflict);
        }

        return ModifyEntityResult<DefaultDeliveryChannel>.Success(defaultDeliveryChannel, WriteResult.Created);
    }
}