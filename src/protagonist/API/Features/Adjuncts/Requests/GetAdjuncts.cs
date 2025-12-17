using API.Infrastructure.Requests;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Page;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Adjuncts.Requests;

public class GetAdjuncts(AssetId assetId) : IRequest<FetchEntityResult<PageOf<Adjunct>>>, IPagedRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    
    public AssetId AssetId { get; } = assetId;
}

public class GetAdjunctsHandler(DlcsContext dbContext) : IRequestHandler<GetAdjuncts, FetchEntityResult<PageOf<Adjunct>>>
{
    public async Task<FetchEntityResult<PageOf<Adjunct>>> Handle(GetAdjuncts request, CancellationToken cancellationToken)
    {
        var result = await dbContext.Adjuncts.AsNoTracking().Where(a => a.AssetId == request.AssetId)
            .CreatePagedResult(request, q => q, q => q.OrderBy(i => i.Id), cancellationToken);
        
        return FetchEntityResult<PageOf<Adjunct>>.Success(result);
    }
}
