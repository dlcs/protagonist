using API.Infrastructure.Requests;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Adjuncts.Requests;

public class GetAdjunct(string id, AssetId assetId) : IRequest<FetchEntityResult<Adjunct>>
{
    public string Id { get; } = id;
    
    public AssetId AssetId { get; } = assetId;
}

public class GetAdjunctHandler(DlcsContext dbContext) : IRequestHandler<GetAdjunct, FetchEntityResult<Adjunct>>
{
    public async Task<FetchEntityResult<Adjunct>> Handle(GetAdjunct request, CancellationToken cancellationToken)
    {
        var adjunct = await dbContext.Adjuncts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == request.Id && a.AssetId == request.AssetId, cancellationToken);
        
        return adjunct == null 
            ? FetchEntityResult<Adjunct>.NotFound() 
            : FetchEntityResult<Adjunct>.Success(adjunct);
    }
}
