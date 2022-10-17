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

    public GetImageOptimisationPolicy(string imageOptimisationPolicyId)
    {
        ImageOptimisationPolicyId = imageOptimisationPolicyId;
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
        var policy = await dlcsContext.ImageOptimisationPolicies.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == request.ImageOptimisationPolicyId,
                cancellationToken);
        return policy == null
            ? FetchEntityResult<ImageOptimisationPolicy>.NotFound()
            : FetchEntityResult<ImageOptimisationPolicy>.Success(policy);
    }
}