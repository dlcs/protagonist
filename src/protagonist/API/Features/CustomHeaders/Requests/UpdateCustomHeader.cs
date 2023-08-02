using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.CustomHeaders.Requests;

public class UpdateCustomHeader : IRequest<ModifyEntityResult<CustomHeader>>
{
    public int CustomerId { get; }
    
    public CustomHeader CustomHeader { get; }
    
    public UpdateCustomHeader(int customerId, CustomHeader customHeader)
    {
        CustomerId = customerId;
        CustomHeader = customHeader;
    }
}

public class UpdateCustomHeaderHandler : IRequestHandler<UpdateCustomHeader, ModifyEntityResult<CustomHeader>>
{  
    private readonly DlcsContext dbContext;

    public UpdateCustomHeaderHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<ModifyEntityResult<CustomHeader>> Handle(UpdateCustomHeader request, CancellationToken cancellationToken)
    {
        var existingCustomHeader = await dbContext.CustomHeaders.SingleOrDefaultAsync(
            ch => ch.Customer == request.CustomerId && ch.Id == request.CustomHeader.Id, cancellationToken);
        
        if (existingCustomHeader == null)
        {
            return ModifyEntityResult<CustomHeader>.Failure($"Couldn't find a custom header with the id {request.CustomHeader.Id}",
                WriteResult.NotFound);
        }

        existingCustomHeader.Role = request.CustomHeader.Role;
        existingCustomHeader.Key = request.CustomHeader.Key;
        existingCustomHeader.Value = request.CustomHeader.Value;
        existingCustomHeader.Space = request.CustomHeader.Space;

        await dbContext.SaveChangesAsync(cancellationToken); 
        
        return ModifyEntityResult<CustomHeader>.Success(existingCustomHeader, WriteResult.Created);
    }
}