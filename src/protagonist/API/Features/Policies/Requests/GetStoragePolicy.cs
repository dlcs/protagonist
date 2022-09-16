using System.Threading;
using System.Threading.Tasks;
using API.Infrastructure.Requests;
using DLCS.Model.Storage;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Policies.Requests;

/// <summary>
/// Get details of specified storage policy
/// </summary>
public class GetStoragePolicy : IRequest<FetchEntityResult<StoragePolicy>>
{
    public string StoragePolicyId { get; }

    public GetStoragePolicy(string storagePolicyId)
    {
        StoragePolicyId = storagePolicyId;
    }
}

public class GetStoragePolicyHandler : IRequestHandler<GetStoragePolicy,
    FetchEntityResult<StoragePolicy>>
{
    private readonly DlcsContext dlcsContext;

    public GetStoragePolicyHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<StoragePolicy>> Handle(GetStoragePolicy request,
        CancellationToken cancellationToken)
    {
        var policy = await dlcsContext.StoragePolicies.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == request.StoragePolicyId,
                cancellationToken);
        return policy == null
            ? FetchEntityResult<StoragePolicy>.NotFound()
            : FetchEntityResult<StoragePolicy>.Success(policy);
    }
}