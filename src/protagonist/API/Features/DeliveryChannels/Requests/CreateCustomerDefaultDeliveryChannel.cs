using System.IO.Enumeration;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.HydraModel;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests;

public class CreateCustomerDefaultDeliveryChannel : IRequest<ModifyEntityResult<CreateDefaultDeliveryChannelResult>>
{
    public int Customer { get; }
    
    public int Space { get; }
    
    public DefaultDeliveryChannel DefaultDeliveryChannel { get; }
    
    public CreateCustomerDefaultDeliveryChannel(int customer, int space, DefaultDeliveryChannel defaultDeliveryChannel)
    {
        Customer = customer;
        DefaultDeliveryChannel = defaultDeliveryChannel;
        Space = space;
    }
}

public class CreateDefaultDeliveryChannelResult
{
    public DLCS.Model.DeliveryChannels.DefaultDeliveryChannel? DefaultDeliveryChannel;
}

public class CreateCustomerDefaultDeliveryChannelHandler : IRequestHandler<CreateCustomerDefaultDeliveryChannel,
    ModifyEntityResult<CreateDefaultDeliveryChannelResult>>
{
    private readonly DlcsContext dbContext;

    public CreateCustomerDefaultDeliveryChannelHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ModifyEntityResult<CreateDefaultDeliveryChannelResult>> Handle(
        CreateCustomerDefaultDeliveryChannel request, CancellationToken cancellationToken)
    {
        var existingPolicy = await dbContext.DefaultDeliveryChannels.AnyAsync(p => 
                p.Customer == request.Customer &&
                p.MediaType == request.DefaultDeliveryChannel.MediaType &&
                p.Space == request.Space,
            cancellationToken);

        if (existingPolicy)
        {
            return ModifyEntityResult<CreateDefaultDeliveryChannelResult>.Failure(
                "Attempting to create a policy that already exists" , 
                WriteResult.Conflict);
        }
        
        var defaultDeliveryChannel = new DLCS.Model.DeliveryChannels.DefaultDeliveryChannel()
        {
            Customer = request.Customer,
            MediaType = request.DefaultDeliveryChannel.MediaType,
            Space = request.Space
        };

        try
        {
            var deliveryChannelPolicy = dbContext.DeliveryChannelPolicies.Single(p =>
                                            p.Customer == request.Customer &&
                                            p.System == false &&
                                            p.Channel == request.DefaultDeliveryChannel
                                                .Channel &&
                                            p.Name == request.DefaultDeliveryChannel.Policy!.Split('/', StringSplitOptions.None).Last() || 
                                            p.Customer == 1 &&
                                            p.System == true &&
                                            p.Channel == request.DefaultDeliveryChannel
                                                .Channel &&
                                            p.Name == request.DefaultDeliveryChannel
                                                .Policy);

            defaultDeliveryChannel.DeliveryChannelPolicyId = deliveryChannelPolicy.Id;
        }
        catch (InvalidOperationException)
        {
            return ModifyEntityResult<CreateDefaultDeliveryChannelResult>.Failure("Failed to find linked delivery channel policy", WriteResult.BadRequest);
        }

        var returnedDeliveryChannel = dbContext.DefaultDeliveryChannels.Add(defaultDeliveryChannel);

        await dbContext.SaveChangesAsync(cancellationToken);

        var created = new CreateDefaultDeliveryChannelResult()
        {
            DefaultDeliveryChannel = returnedDeliveryChannel.Entity
        };

        return ModifyEntityResult<CreateDefaultDeliveryChannelResult>.Success(created, WriteResult.Created);
    }
}