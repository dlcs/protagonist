using API.Infrastructure.Requests;
using API.Infrastructure.Requests.Pipelines;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DefaultDeliveryChannels;

/// <summary>
/// Delete specified defaultDeliveryChannel
/// </summary>
public class DeleteDefaultDeliveryChannel : IRequest<DeleteEntityResult>, IInvalidateCaches
{
    public DeleteDefaultDeliveryChannel(int customer, int space, Guid defaultDeliveryChannelId)
    {
        Customer = customer;
        Space = space;
        DefaultDeliveryChannelId = defaultDeliveryChannelId;
    }
    
    public int Customer { get; }
    
    public int Space { get; }
    
    public Guid DefaultDeliveryChannelId { get; }
    
    public string[] InvalidatedCacheKeys => CacheKeys.DefaultDeliveryChannels(Customer).AsArray();
}

public class DeleteDefaultDeliveryChannelHandler : IRequestHandler<DeleteDefaultDeliveryChannel, DeleteEntityResult>
{
    private readonly DlcsContext dbContext;

    public DeleteDefaultDeliveryChannelHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<DeleteEntityResult> Handle(DeleteDefaultDeliveryChannel request, CancellationToken cancellationToken)
    {
        var defaultDeliveryChannel = await dbContext.DefaultDeliveryChannels.SingleOrDefaultAsync(
            ch => ch.Customer == request.Customer && 
                  ch.Id == request.DefaultDeliveryChannelId &&
                  ch.Space == request.Space,
            cancellationToken: cancellationToken);

        if (defaultDeliveryChannel == null)
        {
            return DeleteEntityResult.Failure(
                $"Deletion failed - Default Delivery Channel {request.DefaultDeliveryChannelId} was not found",
                DeleteResult.NotFound);
        }

        dbContext.DefaultDeliveryChannels.Remove(defaultDeliveryChannel);
        await dbContext.SaveChangesAsync(cancellationToken);

        return DeleteEntityResult.Success;
    }
}