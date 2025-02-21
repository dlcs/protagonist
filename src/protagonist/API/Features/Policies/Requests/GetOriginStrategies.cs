using API.Infrastructure.Requests;
using DLCS.Model.Page;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Policies.Requests;

/// <summary>
/// Get a paged list of all origin strategies
/// </summary>
public class GetOriginStrategies : IRequest<FetchEntityResult<PageOf<OriginStrategy>>>, IPagedRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class GetOriginStrategiesHandler : IRequestHandler<GetOriginStrategies,
    FetchEntityResult<PageOf<OriginStrategy>>>
{
    private readonly DlcsContext dlcsContext;

    public GetOriginStrategiesHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<PageOf<OriginStrategy>>> Handle(GetOriginStrategies request,
        CancellationToken cancellationToken)
    {
        var result = await dlcsContext.OriginStrategies.AsNoTracking().CreatePagedResult(request,
            q => q,
            q => q.OrderBy(i => i.Id),
            cancellationToken: cancellationToken);

        return FetchEntityResult<PageOf<OriginStrategy>>.Success(result);
    }
}