using API.Infrastructure.Requests;
using DLCS.Model.Page;
using DLCS.Model.Storage;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Policies.Requests;

/// <summary>
/// Get a paged list of all storage policies
/// </summary>
public class GetStoragePolicies : IRequest<FetchEntityResult<PageOf<StoragePolicy>>>, IPagedRequest
{
public int Page { get; set; }
public int PageSize { get; set; }
}

public class GetStoragePoliciesHandler : IRequestHandler<GetStoragePolicies, FetchEntityResult<PageOf<StoragePolicy>>>
{
    private readonly DlcsContext dlcsContext;

    public GetStoragePoliciesHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<PageOf<StoragePolicy>>> Handle(GetStoragePolicies request,
        CancellationToken cancellationToken)
    {
        var result = await dlcsContext.StoragePolicies.AsNoTracking().CreatePagedResult(request,
            q => q,
            q => q.OrderBy(i => i.Id),
            cancellationToken: cancellationToken);

        return FetchEntityResult<PageOf<StoragePolicy>>.Success(result);
    }
}