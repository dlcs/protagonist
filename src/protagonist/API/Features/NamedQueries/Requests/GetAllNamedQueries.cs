using System.Collections.Generic;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.NamedQueries.Requests; 

public class GetAllNamedQueries : IRequest<List<DLCS.Model.Assets.NamedQueries.NamedQuery>>
{
    public int CustomerId { get; }
    
    public GetAllNamedQueries(int id)
    {
        CustomerId = id;
    }
}

public class GetAllNamedQueriesHandler : IRequestHandler<GetAllNamedQueries, List<DLCS.Model.Assets.NamedQueries.NamedQuery>>
{
    private readonly DlcsContext dbContext;
    
    public GetAllNamedQueriesHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<List<DLCS.Model.Assets.NamedQueries.NamedQuery>> Handle(GetAllNamedQueries request, CancellationToken cancellationToken)
    {
        return await dbContext.NamedQueries
            .AsNoTracking()
            .Where(nq => nq.Customer == request.CustomerId)
            .ToListAsync(cancellationToken);
    }
}