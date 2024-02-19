using DLCS.Core;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests;

public class DeleteCustomerDefaultDeliveryChannel : IRequest<ResultMessage<DeleteResult>>
{
    public DeleteCustomerDefaultDeliveryChannel(int customer, string defaultDeliveryChannelId)
    {
        Customer = customer;
        DefaultDeliveryChannelId = defaultDeliveryChannelId;
    }
    
    public int Customer { get; }
    
    public string DefaultDeliveryChannelId { get; }
}

public class DeleteCustomHeaderHandler : IRequestHandler<DeleteCustomerDefaultDeliveryChannel, ResultMessage<DeleteResult>>
{
    private readonly DlcsContext dbContext;

    public DeleteCustomHeaderHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ResultMessage<DeleteResult>> Handle(DeleteCustomerDefaultDeliveryChannel request, CancellationToken cancellationToken)
    {
        var isGuid = Guid.TryParse(request.DefaultDeliveryChannelId, out var defaultDeliveryChannelGuid);

        if (!isGuid) return new ResultMessage<DeleteResult>("Could not parse id", DeleteResult.Error);
        
        var customHeader = await dbContext.DefaultDeliveryChannels.SingleOrDefaultAsync(
            ch => ch.Customer == request.Customer && 
                  ch.Id == defaultDeliveryChannelGuid,
            cancellationToken: cancellationToken);

        if (customHeader == null)
        {
            return new ResultMessage<DeleteResult>(
                $"Deletion failed - Default Delivery Channel {request.DefaultDeliveryChannelId} was not found", DeleteResult.NotFound);
        }

        dbContext.DefaultDeliveryChannels.Remove(customHeader);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return new ResultMessage<DeleteResult>(
            $"Default Delivery Channel {request.DefaultDeliveryChannelId} successfully deleted", DeleteResult.Deleted);
    }
}