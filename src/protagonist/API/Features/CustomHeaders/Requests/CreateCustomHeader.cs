using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Model.Assets.NamedQueries;
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

public class CreateNamedQueryHandler : IRequestHandler<CreateCustomHeader, ModifyEntityResult<CustomHeader>>
{  
    private readonly DlcsContext dbContext;

    public CreateNamedQueryHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<ModifyEntityResult<CustomHeader>> Handle(CreateCustomHeader request, CancellationToken cancellationToken)
    {
        var newCustomHeader = new CustomHeader
        {
            Id = Guid.NewGuid().ToString(),
            Customer = request.CustomerId,
            Space = request.CustomHeader.Space,
            Role = request.CustomHeader.Role,
            Key = request.CustomHeader.Key,
            Value = request.CustomHeader.Value
        };

        await dbContext.CustomHeaders.AddAsync(newCustomHeader, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken); 
        
        return ModifyEntityResult<CustomHeader>.Success(newCustomHeader, WriteResult.Created);
    }
}