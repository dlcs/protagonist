using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.NamedQueries.Requests;

public class GetAllNamedQueries : IRequest<FetchEntityResult<IList<NamedQuery>>>
{
    public int CustomerId { get; }
    
    public GetAllNamedQueries(int id)
    {
        CustomerId = id;
    }
}

public class GetAllNamedQueriesHandler : IRequestHandler<GetAllNamedQueries, FetchEntityResult<IList<NamedQuery>>>
{
    private readonly DlcsContext dbContext;
    
    public GetAllNamedQueriesHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<FetchEntityResult<IList<NamedQuery>>> Handle(
        GetAllNamedQueries request, 
        CancellationToken cancellationToken)
    {
        var namedQueries = await dbContext.NamedQueries
            .AsNoTracking()
            .Where(nq => nq.Customer == request.CustomerId)
            .ToListAsync(cancellationToken);
        
        return FetchEntityResult<IList<NamedQuery>>.Success(namedQueries);
    }
}