using API.Features.DeliveryChannels.Validation;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Policies;
using DLCS.Repository;
using DLCS.Repository.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DeliveryChannelPolicies;

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
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.GetDatabaseError() is UniqueConstraintError)
        {
            return ModifyEntityResult<DeliveryChannelPolicy>.Failure(
                $"A {request.DeliveryChannelPolicy.Channel}' policy called '{request.DeliveryChannelPolicy.Name}' already exists",
                WriteResult.Conflict);
        }

        return ModifyEntityResult<DeliveryChannelPolicy>.Success(newDeliveryChannelPolicy, WriteResult.Created);
    }
}


