using API.Infrastructure.Requests;
using DLCS.Model.Page;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Policies.Requests;

/// <summary>
/// Get a paged list of all image optimisation policies.
/// If Customer specified, get customer specific entities AND global. Else get global only
/// </summary>
public class GetImageOptimisationPolicies : IRequest<FetchEntityResult<PageOf<ImageOptimisationPolicy>>>, IPagedRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int? Customer { get; }
    
    public GetImageOptimisationPolicies(int? customer = null)
    {
        Customer = customer;
    }
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
        var filter = GetFilterForRequest(request);

        var result = await dlcsContext.ImageOptimisationPolicies.AsNoTracking().CreatePagedResult(request,
            filter,
            q => q.OrderBy(i => i.Id),
            cancellationToken: cancellationToken);

        return FetchEntityResult<PageOf<ImageOptimisationPolicy>>.Success(result);
    }

    private static Func<IQueryable<ImageOptimisationPolicy>, IQueryable<ImageOptimisationPolicy>> GetFilterForRequest(GetImageOptimisationPolicies request)
    {
        Func<IQueryable<ImageOptimisationPolicy>, IQueryable<ImageOptimisationPolicy>> filter =
            request.Customer.HasValue
                ? policies => policies.Where(p => p.Customer == request.Customer || p.Global)
                : policies => policies.Where(p => p.Global);
        return filter;
    }
}