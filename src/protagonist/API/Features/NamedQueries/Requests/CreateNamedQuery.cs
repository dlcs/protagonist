using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.NamedQueries.Requests;

public class CreateNamedQuery : IRequest<ModifyEntityResult<NamedQuery>>
{
    public int CustomerId { get; }
    
    public NamedQuery NamedQuery { get; }
    
    public CreateNamedQuery(int customerId, NamedQuery namedQuery)
    {
        CustomerId = customerId;
        NamedQuery = namedQuery;
    }
}

public class CreateNamedQueryHandler : IRequestHandler<CreateNamedQuery, ModifyEntityResult<NamedQuery>>
{  
    private readonly DlcsContext dbContext;

    public CreateNamedQueryHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<ModifyEntityResult<NamedQuery>> Handle(CreateNamedQuery request, CancellationToken cancellationToken)
    {
        if (request.CustomerId != request.NamedQuery.Customer)
        {
            return ModifyEntityResult<NamedQuery>.Failure("The user id provided in the named query model does not match the calling user's id",
                WriteResult.FailedValidation);
        }
        
        var existingNamedQuery = await dbContext.NamedQueries.AsNoTracking().SingleOrDefaultAsync(
            nq => nq.Customer == request.CustomerId && nq.Name == request.NamedQuery.Name, cancellationToken);
        
        if (existingNamedQuery != null)
        {
            return ModifyEntityResult<NamedQuery>.Failure("A named query with that name already exists",
                WriteResult.Conflict);
        }

        var newNamedQuery = new NamedQuery()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = request.NamedQuery.Customer,
            Name = request.NamedQuery.Name,
            Global = request.NamedQuery.Global,
            Template = request.NamedQuery.Template
        };

        await dbContext.NamedQueries.AddAsync(newNamedQuery, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken); 
        
        return ModifyEntityResult<NamedQuery>.Success(newNamedQuery, WriteResult.Created);
    }
}