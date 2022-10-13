using API.Infrastructure.Requests;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Policies.Requests;

/// <summary>
/// Get details of specified image optimisation policy
/// </summary>
public class GetThumbnailPolicy : IRequest<FetchEntityResult<ThumbnailPolicy>>
{
    public string ThumbnailPolicyId { get; }

    public GetThumbnailPolicy(string thumbnailPolicyId)
    {
        ThumbnailPolicyId = thumbnailPolicyId;
    }
}

public class GetThumbnailPolicyHandler : IRequestHandler<GetThumbnailPolicy,
    FetchEntityResult<ThumbnailPolicy>>
{
    private readonly DlcsContext dlcsContext;

    public GetThumbnailPolicyHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<ThumbnailPolicy>> Handle(GetThumbnailPolicy request,
        CancellationToken cancellationToken)
    {
        var policy = await dlcsContext.ThumbnailPolicies.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == request.ThumbnailPolicyId,
                cancellationToken);
        return policy == null
            ? FetchEntityResult<ThumbnailPolicy>.NotFound()
            : FetchEntityResult<ThumbnailPolicy>.Success(policy);
    }
}