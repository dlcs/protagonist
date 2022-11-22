using API.Infrastructure.Requests;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Policies.Requests;

/// <summary>
/// Get details of specified image optimisation policy
/// </summary>
public class GetImageOptimisationPolicy : IRequest<FetchEntityResult<ImageOptimisationPolicy>>
{
    public string ImageOptimisationPolicyId { get; }
    public int? Customer { get; }

    public GetImageOptimisationPolicy(string imageOptimisationPolicyId, int? customer = null)
    {
        ImageOptimisationPolicyId = imageOptimisationPolicyId;
        Customer = customer;
    }
}

public class GetImageOptimisationPolicyHandler : IRequestHandler<GetImageOptimisationPolicy,
    FetchEntityResult<ImageOptimisationPolicy>>
{
    private readonly DlcsContext dlcsContext;

    public GetImageOptimisationPolicyHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<ImageOptimisationPolicy>> Handle(GetImageOptimisationPolicy request,
        CancellationToken cancellationToken)
    {
        var filteredPolicies = GetFilteredPolicies(request);

        var policy = await filteredPolicies
            .SingleOrDefaultAsync(b => b.Id == request.ImageOptimisationPolicyId,
                cancellationToken);
        return policy == null
            ? FetchEntityResult<ImageOptimisationPolicy>.NotFound()
            : FetchEntityResult<ImageOptimisationPolicy>.Success(policy);
    }

    private IQueryable<ImageOptimisationPolicy> GetFilteredPolicies(GetImageOptimisationPolicy request)
    {
        var imageOptimisationPolicies = dlcsContext.ImageOptimisationPolicies.AsNoTracking();

        imageOptimisationPolicies = request.Customer.HasValue
            ? imageOptimisationPolicies.Where(p => p.Customer == request.Customer || p.Global)
            : imageOptimisationPolicies.Where(p => p.Global);
        return imageOptimisationPolicies;
    }
}