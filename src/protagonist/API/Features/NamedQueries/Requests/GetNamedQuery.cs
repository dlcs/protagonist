using System.Collections.Generic;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.NamedQueries.Requests; 

public class GetNamedQuery : IRequest<DLCS.Model.Assets.NamedQueries.NamedQuery?>
{
    public int CustomerId { get; }
    
    public string NamedQueryId { get; }
    
    public GetNamedQuery(int id, string namedQueryId)
    {
        CustomerId = id;
        NamedQueryId = namedQueryId;
    }
}

public class GetNamedQueryHandler : IRequestHandler<GetNamedQuery, DLCS.Model.Assets.NamedQueries.NamedQuery?>
{
    private readonly DlcsContext dbContext;
    
    public GetNamedQueryHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<DLCS.Model.Assets.NamedQueries.NamedQuery?> Handle(GetNamedQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.NamedQueries
            .AsNoTracking()
            .SingleOrDefaultAsync(nq => nq.Customer == request.CustomerId
                                       && nq.Id == request.NamedQueryId, cancellationToken);
    }
}