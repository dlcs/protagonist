using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.NamedQueries.Requests;

public class UpdateNamedQuery : IRequest<ModifyEntityResult<NamedQuery>>
{
    public int CustomerId { get; }
    
    public NamedQuery NamedQuery { get; }

    
    public UpdateNamedQuery(int customerId, NamedQuery namedQuery)
    {
        CustomerId = customerId;
        NamedQuery = namedQuery;
    }
}

public class UpdateNamedQueryHandler : IRequestHandler<UpdateNamedQuery, ModifyEntityResult<NamedQuery>>
{
    private readonly DlcsContext dbContext;

    public UpdateNamedQueryHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ModifyEntityResult<NamedQuery>> Handle(UpdateNamedQuery request, CancellationToken cancellationToken)
    {
        var existingNamedQuery = await dbContext.NamedQueries.SingleOrDefaultAsync(
            nq => nq.Customer == request.CustomerId && nq.Id == request.NamedQuery.Id, cancellationToken);
        
        if (existingNamedQuery == null)
        {
            return ModifyEntityResult<NamedQuery>.Failure($"Couldn't find a named query with the id {request.NamedQuery.Id}",
                WriteResult.NotFound);
        }
        
        existingNamedQuery.Template = request.NamedQuery.Template;
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return ModifyEntityResult<NamedQuery>.Success(existingNamedQuery);
    }
}