using API.Features.DeliveryChannels.Validation;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests;

public class UpdateDeliveryChannelPolicy : IRequest<ModifyEntityResult<DeliveryChannelPolicy>>
{
    public int CustomerId { get; }
    
    public DeliveryChannelPolicy DeliveryChannelPolicy { get; }
    
    public UpdateDeliveryChannelPolicy(int customerId, DeliveryChannelPolicy deliveryChannelPolicy)
    {
        CustomerId = customerId;
        DeliveryChannelPolicy = deliveryChannelPolicy;
    }
}

public class UpdateDeliveryChannelPolicyHandler : IRequestHandler<UpdateDeliveryChannelPolicy, ModifyEntityResult<DeliveryChannelPolicy>>
{
    private readonly DlcsContext dbContext;
    private readonly DeliveryChannelPolicyDataValidator policyDataValidator;
    
    public UpdateDeliveryChannelPolicyHandler(DlcsContext dbContext, DeliveryChannelPolicyDataValidator policyDataValidator)
    {
        this.dbContext = dbContext;
        this.policyDataValidator = policyDataValidator;
    }
    
    public async Task<ModifyEntityResult<DeliveryChannelPolicy>> Handle(UpdateDeliveryChannelPolicy request, CancellationToken cancellationToken)
    {
        if(!policyDataValidator.Validate(request.DeliveryChannelPolicy.PolicyData, request.DeliveryChannelPolicy.Channel))
        {
            return ModifyEntityResult<DeliveryChannelPolicy>.Failure(
                $"'policyData' contains bad JSON or invalid data", 
                WriteResult.FailedValidation);
        }
        
        var existingDeliveryChannelPolicy = await dbContext.DeliveryChannelPolicies.SingleOrDefaultAsync(p => 
            p.Customer == request.CustomerId &&
            p.Channel == request.DeliveryChannelPolicy.Channel &&
            p.Name == request.DeliveryChannelPolicy.Name,
            cancellationToken);

        if (existingDeliveryChannelPolicy != null)
        {
            existingDeliveryChannelPolicy.DisplayName = request.DeliveryChannelPolicy.DisplayName;
            existingDeliveryChannelPolicy.Modified = DateTime.UtcNow;
            existingDeliveryChannelPolicy.PolicyData = request.DeliveryChannelPolicy.PolicyData;
            
            await dbContext.SaveChangesAsync(cancellationToken); 
    
            return ModifyEntityResult<DeliveryChannelPolicy>.Success(existingDeliveryChannelPolicy);
        }
        else
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
            await dbContext.SaveChangesAsync(cancellationToken); 
            
            return ModifyEntityResult<DeliveryChannelPolicy>.Success(newDeliveryChannelPolicy, WriteResult.Created);
        }
    }
}