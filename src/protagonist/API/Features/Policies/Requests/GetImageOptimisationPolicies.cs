using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Infrastructure.Requests;
using DLCS.Model.Page;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Policies.Requests;

/// <summary>
/// Get a paged list of all image optimisation policies
/// </summary>
public class GetImageOptimisationPolicies : IRequest<FetchEntityResult<PageOf<ImageOptimisationPolicy>>>, IPagedRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class GetImageOptimisationPoliciesHandler : IRequestHandler<GetImageOptimisationPolicies,
    FetchEntityResult<PageOf<ImageOptimisationPolicy>>>
{
    private readonly DlcsContext dlcsContext;

    public GetImageOptimisationPoliciesHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<PageOf<ImageOptimisationPolicy>>> Handle(GetImageOptimisationPolicies request,
        CancellationToken cancellationToken)
    {
        var result = await dlcsContext.ImageOptimisationPolicies.AsNoTracking().CreatePagedResult(request,
            q => q,
            q => q.OrderBy(i => i.Id),
            cancellationToken: cancellationToken);

        return FetchEntityResult<PageOf<ImageOptimisationPolicy>>.Success(result);
    }
}