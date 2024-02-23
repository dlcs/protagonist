using API.Features.DeliveryChannels.Validation;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests;

public class CreateDeliveryChannelPolicy : IRequest<ModifyEntityResult<DeliveryChannelPolicy>>
{
    public int CustomerId { get; }
    
    public DeliveryChannelPolicy DeliveryChannelPolicy { get; }
    
    public CreateDeliveryChannelPolicy(int customerId, DeliveryChannelPolicy deliveryChannelPolicy)
    {
        CustomerId = customerId;
        DeliveryChannelPolicy = deliveryChannelPolicy;
    }
}

public class CreateDeliveryChannelPolicyHandler : IRequestHandler<CreateDeliveryChannelPolicy, ModifyEntityResult<DeliveryChannelPolicy>>
{
    private readonly DlcsContext dbContext;
    
    public CreateDeliveryChannelPolicyHandler(DlcsContext dbContext, DeliveryChannelPolicyDataValidator policyDataValidator)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<ModifyEntityResult<DeliveryChannelPolicy>> Handle(CreateDeliveryChannelPolicy request, CancellationToken cancellationToken)
    {
        var nameInUse = await dbContext.DeliveryChannelPolicies.AnyAsync(p => 
            p.Customer == request.CustomerId &&
            p.Channel == request.DeliveryChannelPolicy.Channel &&
            p.Name == request.DeliveryChannelPolicy.Name,
            cancellationToken);
        
        if (nameInUse)
        {
            return ModifyEntityResult<DeliveryChannelPolicy>.Failure(
                $"A {request.DeliveryChannelPolicy.Channel}' policy called '{request.DeliveryChannelPolicy.Name}' already exists" , 
                WriteResult.Conflict);
        }
        
        var newDeliveryChannelPolicy = new DeliveryChannelPolicy()
        {
            Customer = request.CustomerId,
            Name = request.DeliveryChannelPolicy.Name,
            DisplayName = request.DeliveryChannelPolicy.DisplayName,
            Channel = request.DeliveryChannelPolicy.Channel, 
            System = false,
            Modified = DateTime.UtcNow,
            Created = DateTime.UtcNow,
            PolicyData = request.DeliveryChannelPolicy.PolicyData,
        };
        
        await dbContext.DeliveryChannelPolicies.AddAsync(newDeliveryChannelPolicy, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken); 
        
        return ModifyEntityResult<DeliveryChannelPolicy>.Success(newDeliveryChannelPolicy, WriteResult.Created);
    }
}


