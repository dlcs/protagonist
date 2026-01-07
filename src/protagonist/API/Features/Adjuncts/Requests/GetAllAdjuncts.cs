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
        var asset = await dbContext.Images.Include(i => i.Adjuncts)
            .FirstOrDefaultAsync(i => i.Id == request.AssetId, cancellationToken);

        if (asset == null) return FetchEntityResult<IReadOnlyCollection<Adjunct>>.NotFound();

        return FetchEntityResult<IReadOnlyCollection<Adjunct>>.Success(
            asset.Adjuncts?.OrderBy(a => a.Id).ToList() ?? []);
    }
}
