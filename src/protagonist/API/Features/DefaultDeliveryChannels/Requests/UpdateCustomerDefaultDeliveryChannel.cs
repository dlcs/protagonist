using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.DeliveryChannels;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DefaultDeliveryChannels.Requests;

public class UpdateCustomerDefaultDeliveryChannel : IRequest<ModifyEntityResult<DefaultDeliveryChannel>>
{
    public int Customer { get; }
    
    public DefaultDeliveryChannel DefaultDeliveryChannel { get; }

    public UpdateCustomerDefaultDeliveryChannel(int customerId, DefaultDeliveryChannel defaultDeliveryChannel)
    {
        Customer = customerId;
        
        DefaultDeliveryChannel = defaultDeliveryChannel;
    }
}

public class UpdateCustomHeaderHandler : IRequestHandler<UpdateCustomerDefaultDeliveryChannel, ModifyEntityResult<DefaultDeliveryChannel>>
{  
    private readonly DlcsContext dbContext;

    public UpdateCustomHeaderHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<ModifyEntityResult<DefaultDeliveryChannel>> Handle(UpdateCustomerDefaultDeliveryChannel request, CancellationToken cancellationToken)
    {
        var existingCustomHeader = await dbContext.DefaultDeliveryChannels.SingleOrDefaultAsync(
            d => d.Customer == request.Customer && d.Id == request.DefaultDeliveryChannel.Id, cancellationToken);
        
        if (existingCustomHeader == null)
        {
            return ModifyEntityResult<DefaultDeliveryChannel>.Failure($"Couldn't find a custom header with the id {request.DefaultDeliveryChannel.Id}",
                WriteResult.NotFound);
        }

        existingCustomHeader.MediaType = request.DefaultDeliveryChannel.MediaType;
        existingCustomHeader.DeliveryChannelPolicyId = request.DefaultDeliveryChannel.DeliveryChannelPolicyId;

        await dbContext.SaveChangesAsync(cancellationToken); 
        
        return ModifyEntityResult<DefaultDeliveryChannel>.Success(existingCustomHeader, WriteResult.Created);
    }
}