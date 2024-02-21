using API.Infrastructure.Requests;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.CustomHeaders.Requests; 

public class GetCustomHeader : IRequest<FetchEntityResult<CustomHeader>>
{
    public int CustomerId { get; }
    
    public string CustomHeaderId { get; }
    
    public GetCustomHeader(int id, string namedQueryId)
    {
        CustomerId = id;
        CustomHeaderId = namedQueryId;
    }
}

public class GetCustomHeaderHandler : IRequestHandler<GetCustomHeader, FetchEntityResult<CustomHeader>>
{
    private readonly DlcsContext dbContext;
    
    public GetCustomHeaderHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<FetchEntityResult<CustomHeader>> Handle(GetCustomHeader request, CancellationToken cancellationToken)
    {
        var customHeader = await dbContext.CustomHeaders
            .AsNoTracking()
            .SingleOrDefaultAsync(ch => ch.Customer == request.CustomerId
                                        && ch.Id == request.CustomHeaderId, cancellationToken);
        return customHeader == null
            ? FetchEntityResult<CustomHeader>.NotFound()
            : FetchEntityResult<CustomHeader>.Success(customHeader);
    }
}