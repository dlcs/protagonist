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
using Microsoft.Extensions.Logging;

namespace API.Features.DeliveryChannels.Requests.DefaultDeliveryChannels;

/// <summary>
/// Create a new DefaultDeliveryChannel object in DB
/// </summary>
public class CreateDefaultDeliveryChannel : IRequest<ModifyEntityResult<DefaultDeliveryChannel>>, IInvalidateCaches
{
    public int Customer { get; }
    
    public int? Space { get; }

    public string Policy { get; }

    public string Channel { get; }

    public string MediaType { get; }

    public CreateDefaultDeliveryChannel(int customer, int? space, string policy, string channel, string mediaType)
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
    private readonly ILogger<CreateDefaultDeliveryChannelHandler> logger;

    public CreateDefaultDeliveryChannelHandler(DlcsContext dbContext, ILogger<CreateDefaultDeliveryChannelHandler> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    public async Task<ModifyEntityResult<DefaultDeliveryChannel>> Handle(
        CreateDefaultDeliveryChannel request, CancellationToken cancellationToken)
    {
        var spaceZeroError = DefaultDeliveryChannelHelper.GetSpaceZeroErrorMessage(request.Space, SpaceZeroOperation.Create);
        if (spaceZeroError != null) return ModifyEntityResult<DefaultDeliveryChannel>.Failure(spaceZeroError, WriteResult.BadRequest);

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

        var space = request.Space;
        var duplicate = await dbContext.DefaultDeliveryChannels.AnyAsync(
            d => d.Customer == request.Customer &&
                 d.Space == space &&
                 d.MediaType == request.MediaType &&
                 d.DeliveryChannelPolicyId == defaultDeliveryChannel.DeliveryChannelPolicyId,
            cancellationToken);

        if (duplicate)
        {
            return ModifyEntityResult<DefaultDeliveryChannel>.Failure(
                $"A default delivery channel for the requested media type '{defaultDeliveryChannel.MediaType}' already exists",
                WriteResult.Conflict);
        }

        dbContext.DefaultDeliveryChannels.Add(defaultDeliveryChannel);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.GetDatabaseError() is UniqueConstraintError)
        {
            // Race condition: duplicate slipped through the pre-check
            return ModifyEntityResult<DefaultDeliveryChannel>.Failure(
                $"A default delivery channel for the requested media type '{defaultDeliveryChannel.MediaType}' already exists",
                WriteResult.Conflict);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save default delivery channel for customer {Customer}", request.Customer);
            return ModifyEntityResult<DefaultDeliveryChannel>.Failure(
                "Unknown error trying to save the default delivery channel",
                WriteResult.Error);
        }

        return ModifyEntityResult<DefaultDeliveryChannel>.Success(defaultDeliveryChannel, WriteResult.Created);
    }
}
