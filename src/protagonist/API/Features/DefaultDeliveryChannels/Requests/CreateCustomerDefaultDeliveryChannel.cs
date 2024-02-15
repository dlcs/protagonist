using System.Collections.Generic;
using DLCS.HydraModel;
using DLCS.Repository;
using MediatR;

namespace API.Features.DefaultDeliveryChannels.Requests;

public class CreateCustomerDefaultDeliveryChannel : IRequest<CreateDefaultDeliveryChannelResult>
{
    public int Customer { get; }
    
    public DefaultDeliveryChannel DefaultDeliveryChannel { get; }
    
    public CreateCustomerDefaultDeliveryChannel(int customer, DefaultDeliveryChannel defaultDeliveryChannel)
    {
        Customer = customer;
        DefaultDeliveryChannel = defaultDeliveryChannel;
    }
}

public class CreateDefaultDeliveryChannelResult
{
    public DLCS.Model.DeliveryChannels.DefaultDeliveryChannel? DefaultDeliveryChannel;
    public List<string> ErrorMessages = new();
    public bool Conflict { get; set; }
}

public class CreateCustomerDefaultDeliveryChannelHandler : IRequestHandler<CreateCustomerDefaultDeliveryChannel, CreateDefaultDeliveryChannelResult>
{
    private readonly DlcsContext dbContext;

    public CreateCustomerDefaultDeliveryChannelHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<CreateDefaultDeliveryChannelResult> Handle(CreateCustomerDefaultDeliveryChannel request, CancellationToken cancellationToken)
    {
        var defaultDeliveryChannel = new DLCS.Model.DeliveryChannels.DefaultDeliveryChannel()
        {
            Customer = request.Customer,
            DeliveryChannelPolicyId = GetPolicyIdFromRequest(request),
            MediaType = request.DefaultDeliveryChannel.MediaType,
            Space = 0
        };

        var returnedDeliveryChannel = dbContext.DefaultDeliveryChannels.Add(defaultDeliveryChannel);
        
        //await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateDefaultDeliveryChannelResult()
        {
            DefaultDeliveryChannel = returnedDeliveryChannel.Entity
        };
    }

    private int GetPolicyIdFromRequest(CreateCustomerDefaultDeliveryChannel defaultDeliveryChannelCreationRequest)
    {
        var deliveryChannelPolicy = dbContext.DeliveryChannelPolicies.FirstOrDefault(p =>
                                        p.Customer == defaultDeliveryChannelCreationRequest.Customer &&
                                        p.Channel == defaultDeliveryChannelCreationRequest.DefaultDeliveryChannel.Channel &&
                                        p.Name == defaultDeliveryChannelCreationRequest.DefaultDeliveryChannel.Policy) ??
                                    dbContext.DeliveryChannelPolicies.First(p =>
                                        p.Customer == 1 &&
                                        p.System == true &&
                                        p.Channel == defaultDeliveryChannelCreationRequest.DefaultDeliveryChannel.Channel &&
                                        p.Name == defaultDeliveryChannelCreationRequest.DefaultDeliveryChannel.Policy);
        
        return deliveryChannelPolicy.Id; // TODO: change this to correctly retrieve a policy id
    }
}