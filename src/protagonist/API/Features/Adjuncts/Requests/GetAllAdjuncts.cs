using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Adjuncts.Requests;

public class GetAllAdjuncts(AssetId assetId) : IRequest<FetchEntityResult<IReadOnlyCollection<Adjunct>>>
{
    public AssetId AssetId { get; } = assetId;
}

public class GetAllAdjunctsHandler(DlcsContext dbContext) : IRequestHandler<GetAllAdjuncts, FetchEntityResult<IReadOnlyCollection<Adjunct>>>
{
    public async Task<FetchEntityResult<IReadOnlyCollection<Adjunct>>> Handle(GetAllAdjuncts request, CancellationToken cancellationToken)
    {
        var assetExists = dbContext.Images.Any(i => i.Id == request.AssetId);

        if (!assetExists) return FetchEntityResult<IReadOnlyCollection<Adjunct>>.NotFound();
        
        var adjuncts = await dbContext.Adjuncts.AsNoTracking().Where(a => a.AssetId == request.AssetId)
            .ToListAsync(cancellationToken);
        
        return FetchEntityResult<IReadOnlyCollection<Adjunct>>.Success(adjuncts);
    }
}
