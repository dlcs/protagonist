using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.CustomHeaders.Requests;

public class GetAllCustomHeaders : IRequest<FetchEntityResult<IReadOnlyCollection<CustomHeader>>>
{
    public int CustomerId { get; }
    
    public GetAllCustomHeaders(int id)
    {
        CustomerId = id;
    }
}

public class GetAllCustomHeadersHandler : IRequestHandler<GetAllCustomHeaders, FetchEntityResult<IReadOnlyCollection<CustomHeader>>>
{
    private readonly DlcsContext dbContext;
    
    public GetAllCustomHeadersHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<FetchEntityResult<IReadOnlyCollection<CustomHeader>>> Handle(
        GetAllCustomHeaders request, 
        CancellationToken cancellationToken)
    {
        var customHeaders = await dbContext.CustomHeaders
            .AsNoTracking()
            .Where(ch => ch.Customer == request.CustomerId)
            .ToListAsync(cancellationToken);
        
        return FetchEntityResult<IReadOnlyCollection<CustomHeader>>.Success(customHeaders);
    }
}