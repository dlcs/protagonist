using API.Features.DeliveryChannels.Converters;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.HydraModel;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests;

public class UpdateCustomerDefaultDeliveryChannel : IRequest<ModifyEntityResult<UpdateDefaultDeliveryChannelResult>>
{
    public int Customer { get; }

    public int Space;
    
    public DefaultDeliveryChannel DefaultDeliveryChannel { get; }

    public UpdateCustomerDefaultDeliveryChannel(int customerId, int space, DefaultDeliveryChannel defaultDeliveryChannel)
    {
        Customer = customerId;
        DefaultDeliveryChannel = defaultDeliveryChannel;
        Space = space;
    }
}

public class UpdateDefaultDeliveryChannelResult
{
    public DLCS.Model.DeliveryChannels.DefaultDeliveryChannel? DefaultDeliveryChannel;
}

public class UpdateCustomHeaderHandler : IRequestHandler<UpdateCustomerDefaultDeliveryChannel, ModifyEntityResult<UpdateDefaultDeliveryChannelResult>>
{  
    private readonly DlcsContext dbContext;

    public UpdateCustomHeaderHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<ModifyEntityResult<UpdateDefaultDeliveryChannelResult>> Handle(UpdateCustomerDefaultDeliveryChannel request, CancellationToken cancellationToken)
    {
        var convertedDeliveryChannel = request.DefaultDeliveryChannel.ToDlcsModelWithoutPolicy(request.Space, request.Customer);
        
        var defaultDeliveryChannel = await dbContext.DefaultDeliveryChannels.SingleOrDefaultAsync(
            d => d.Customer == request.Customer && d.Id == convertedDeliveryChannel.Id, cancellationToken);
        
        if (defaultDeliveryChannel == null)
        {
            return ModifyEntityResult<UpdateDefaultDeliveryChannelResult>.Failure($"Couldn't find a default delivery channel with the id {request.DefaultDeliveryChannel.Id}",
                WriteResult.NotFound);
        }

        defaultDeliveryChannel.MediaType = request.DefaultDeliveryChannel.MediaType;

        if (request.DefaultDeliveryChannel.Policy != null)
        {
            try
            {
                var deliveryChannelPolicy = dbContext.DeliveryChannelPolicies.SingleOrDefault(p =>
                                                p.Customer == request.Customer &&
                                                p.System == false &&
                                                p.Channel == request.DefaultDeliveryChannel.Channel &&
                                                p.Name == request.DefaultDeliveryChannel.Policy!
                                                    .Split('/', StringSplitOptions.None).Last()) ??
                                            dbContext.DeliveryChannelPolicies.Single(p =>
                                                p.Customer == 1 &&
                                                p.System == true &&
                                                p.Channel == request.DefaultDeliveryChannel.Channel &&
                                                p.Name == request.DefaultDeliveryChannel.Policy);

                defaultDeliveryChannel.DeliveryChannelPolicyId = deliveryChannelPolicy.Id;
            }
            catch (InvalidOperationException)
            {
                return ModifyEntityResult<UpdateDefaultDeliveryChannelResult>.Failure("Failed to find linked delivery channel policy", WriteResult.BadRequest);
            }
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