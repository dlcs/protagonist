using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.NamedQueries.Requests; 

public class GetNamedQuery : IRequest<FetchEntityResult<DLCS.Model.Assets.NamedQueries.NamedQuery>>
{
    public int CustomerId { get; }
    
    public string NamedQueryId { get; }
    
    public GetNamedQuery(int id, string namedQueryId)
    {
        CustomerId = id;
        NamedQueryId = namedQueryId;
    }
}

public class GetNamedQueryHandler : IRequestHandler<GetNamedQuery, FetchEntityResult<DLCS.Model.Assets.NamedQueries.NamedQuery>>
{
    private readonly DlcsContext dbContext;
    
    public GetNamedQueryHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<FetchEntityResult<DLCS.Model.Assets.NamedQueries.NamedQuery>> Handle(GetNamedQuery request, CancellationToken cancellationToken)
    {
        var namedQuery = await dbContext.NamedQueries
            .AsNoTracking()
            .SingleOrDefaultAsync(nq => nq.Customer == request.CustomerId
                                        && nq.Id == request.NamedQueryId, cancellationToken);
        return namedQuery == null
            ? FetchEntityResult<DLCS.Model.Assets.NamedQueries.NamedQuery>.NotFound()
            : FetchEntityResult<DLCS.Model.Assets.NamedQueries.NamedQuery>.Success(namedQuery);
    }
}