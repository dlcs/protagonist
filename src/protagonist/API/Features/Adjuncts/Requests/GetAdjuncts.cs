using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Page;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Adjuncts.Requests;

public class GetAdjuncts(AssetId assetId) : IRequest<FetchEntityResult<IReadOnlyCollection<Adjunct>>>, IPagedRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    
    public AssetId AssetId { get; } = assetId;
}

public class GetAdjunctsHandler(DlcsContext dbContext) : IRequestHandler<GetAdjuncts, FetchEntityResult<IReadOnlyCollection<Adjunct>>>
{
    public async Task<FetchEntityResult<IReadOnlyCollection<Adjunct>>> Handle(GetAdjuncts request, CancellationToken cancellationToken)
    {
        var adjuncts = await dbContext.Adjuncts.AsNoTracking().Where(a => a.AssetId == request.AssetId)
            .ToListAsync(cancellationToken);
        
        return FetchEntityResult<IReadOnlyCollection<Adjunct>>.Success(adjuncts);
    }
}
