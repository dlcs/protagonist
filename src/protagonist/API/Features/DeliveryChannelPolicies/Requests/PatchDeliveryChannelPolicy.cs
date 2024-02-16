using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Core.Strings;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannelPolicies.Requests;

public class PatchDeliveryChannelPolicy : IRequest<ModifyEntityResult<DeliveryChannelPolicy>>
{
    public int CustomerId { get; }

    public string Channel { get; set; }
    
    public string Name { get; set; }
    
    public string? DisplayName { get; set; }

    public string? PolicyData { get; set; }
    
    public PatchDeliveryChannelPolicy(int customerId, string channel, string name)
    {
        CustomerId = customerId;
        Channel = channel;
        Name = name;
    }
}

public class PatchDeliveryChannelPolicyHandler : IRequestHandler<PatchDeliveryChannelPolicy, ModifyEntityResult<DeliveryChannelPolicy>>
{
    private readonly DlcsContext dbContext;
    
    public PatchDeliveryChannelPolicyHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ModifyEntityResult<DeliveryChannelPolicy>> Handle(PatchDeliveryChannelPolicy request,
        CancellationToken cancellationToken)
    {
        var existingDeliveryChannelPolicy = await dbContext.DeliveryChannelPolicies.SingleOrDefaultAsync(p =>
                p.Customer == request.CustomerId &&
                p.Channel == request.Channel &&
                p.Name == request.Name,
            cancellationToken);

        if (existingDeliveryChannelPolicy == null)
        {
            return ModifyEntityResult<DeliveryChannelPolicy>.Failure(
                $"A policy for delivery channel '{request.Channel}' called '{request.Name}' was not found" , 
                WriteResult.NotFound);
        }
        
        var hasBeenChanged = false;
            
        if (request.DisplayName.HasText())
        {
            existingDeliveryChannelPolicy.DisplayName = request.DisplayName;
            hasBeenChanged = true;
        }
            
        if (request.PolicyData.HasText()) {
            existingDeliveryChannelPolicy.PolicyData = request.PolicyData;
            hasBeenChanged = true;
        }
            
        if (hasBeenChanged)
        {
            existingDeliveryChannelPolicy.Modified = DateTime.UtcNow;
        }
        
        var rowCount = await dbContext.SaveChangesAsync(cancellationToken);
        if (rowCount == 0)
        {
            return ModifyEntityResult<DeliveryChannelPolicy>.Failure("Unable to patch delivery channel policy", WriteResult.Error);
        }
        
        return ModifyEntityResult<DeliveryChannelPolicy>.Success(existingDeliveryChannelPolicy);
    }
}