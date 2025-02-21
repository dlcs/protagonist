using API.Infrastructure.Requests;
using DLCS.Model.Page;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Policies.Requests;

/// <summary>
/// Get a paged list of all thumbnail policies
/// </summary>
public class GetThumbnailPolicies : IRequest<FetchEntityResult<PageOf<ThumbnailPolicy>>>, IPagedRequest
{
public int Page { get; set; }
public int PageSize { get; set; }
}

public class GetThumbnailPoliciesHandler 
    : IRequestHandler<GetThumbnailPolicies, FetchEntityResult<PageOf<ThumbnailPolicy>>>
{
    private readonly DlcsContext dlcsContext;

    public GetThumbnailPoliciesHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<PageOf<ThumbnailPolicy>>> Handle(GetThumbnailPolicies request,
        CancellationToken cancellationToken)
    {
        var result = await dlcsContext.ThumbnailPolicies.AsNoTracking().CreatePagedResult(request,
            q => q,
            q => q.OrderBy(i => i.Id),
            cancellationToken: cancellationToken);

        return FetchEntityResult<PageOf<ThumbnailPolicy>>.Success(result);
    }
}