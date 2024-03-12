using DLCS.Core;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DefaultDeliveryChannels;

public class DeleteDefaultDeliveryChannel : IRequest<ResultMessage<DeleteResult>>
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
}

public class DeleteDefaultDeliveryChannelHandler : IRequestHandler<DeleteDefaultDeliveryChannel, ResultMessage<DeleteResult>>
{
    private readonly DlcsContext dbContext;

    public DeleteDefaultDeliveryChannelHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ResultMessage<DeleteResult>> Handle(DeleteDefaultDeliveryChannel request, CancellationToken cancellationToken)
    {
        var defaultDeliveryChannel = await dbContext.DefaultDeliveryChannels.SingleOrDefaultAsync(
            ch => ch.Customer == request.Customer && 
                  ch.Id == request.DefaultDeliveryChannelId &&
                  ch.Space == request.Space,
            cancellationToken: cancellationToken);

        if (defaultDeliveryChannel == null)
        {
            return new ResultMessage<DeleteResult>(
                $"Deletion failed - Default Delivery Channel {request.DefaultDeliveryChannelId} was not found", DeleteResult.NotFound);
        }

        dbContext.DefaultDeliveryChannels.Remove(defaultDeliveryChannel);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return new ResultMessage<DeleteResult>(
            $"Default Delivery Channel {request.DefaultDeliveryChannelId} successfully deleted", DeleteResult.Deleted);
    }
}