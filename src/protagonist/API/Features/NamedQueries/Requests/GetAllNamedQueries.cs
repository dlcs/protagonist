using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.NamedQueries.Requests;

public class GetAllNamedQueries : IRequest<FetchEntityResult<IReadOnlyCollection<NamedQuery>>>
{
    public int CustomerId { get; }
    
    public GetAllNamedQueries(int id)
    {
        CustomerId = id;
    }
}

public class GetAllNamedQueriesHandler : IRequestHandler<GetAllNamedQueries, FetchEntityResult<IReadOnlyCollection<NamedQuery>>>
{
    private readonly DlcsContext dbContext;
    
    public GetAllNamedQueriesHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<FetchEntityResult<IReadOnlyCollection<NamedQuery>>> Handle(
        GetAllNamedQueries request, 
        CancellationToken cancellationToken)
    {
        var namedQueries = await dbContext.NamedQueries
            .AsNoTracking()
            .Where(nq => nq.Customer == request.CustomerId || nq.Global)
            .ToListAsync(cancellationToken);
        
        return FetchEntityResult<IReadOnlyCollection<NamedQuery>>.Success(namedQueries);
    }
}