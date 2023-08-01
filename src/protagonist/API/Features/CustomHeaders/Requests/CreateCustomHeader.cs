using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.CustomHeaders.Requests;

public class CreateCustomHeader : IRequest<ModifyEntityResult<CustomHeader>>
{
    public int CustomerId { get; }
    
    public CustomHeader CustomHeader { get; }
    
    public CreateCustomHeader(int customerId, CustomHeader customHeader)
    {
        CustomerId = customerId;
        CustomHeader = customHeader;
    }
}

public class CreateCustomHeaderHandler : IRequestHandler<CreateCustomHeader, ModifyEntityResult<CustomHeader>>
{  
    private readonly DlcsContext dbContext;

    public CreateCustomHeaderHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<ModifyEntityResult<CustomHeader>> Handle(CreateCustomHeader request, CancellationToken cancellationToken)
    {
        var newCustomHeader = new CustomHeader
        {
            Id = Guid.NewGuid().ToString(),
            Customer = request.CustomerId,
            Role = request.CustomHeader.Role,
            Key = request.CustomHeader.Key,
            Value = request.CustomHeader.Value
        };
        
        if (request.CustomHeader.Space.HasValue)
        {
            var spaceFound =
                dbContext.Spaces.Any(s => s.Customer == request.CustomerId && s.Id == request.CustomHeader.Space);
            
            if (!spaceFound)
            {
                return ModifyEntityResult<CustomHeader>
                    .Failure($"The specified space ({request.CustomHeader.Space}) was not found.", WriteResult.NotFound); 
            }
            
            newCustomHeader.Space = request.CustomHeader.Space;
        }

        await dbContext.CustomHeaders.AddAsync(newCustomHeader, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken); 
        
        return ModifyEntityResult<CustomHeader>.Success(newCustomHeader, WriteResult.Created);
    }
}