using API.Features.Assets.Query;
using API.Infrastructure.Page;
using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Queues.Requests;

public abstract class GetBatchAssetsBase<T>(DlcsContext dlcsContext)
    : IRequestHandler<T, FetchEntityResult<PageOf<Asset>>>
    where T : GetBatchAssets
{
    protected abstract IQueryable<Asset> GetBatchAssets(DlcsContext dlcsContext, T request);

    public async Task<FetchEntityResult<PageOf<Asset>>> Handle(
        T request, CancellationToken cancellationToken)
    {
        var result = await GetBatchAssets(dlcsContext, request).CreatePagedResult(
            request,
            i => i
                .ApplyAssetFilter(request.AssetQueryModel.Filter, true)
                .AsSingleQuery(),
            images => images.AsOrderedAssetQuery(request),
            cancellationToken);

        // Any empty result set could be the result of an applied asset filter - check if batch exists
        if (result.Total == 0 && !await DoesBatchExist(request, cancellationToken))
        {
            return FetchEntityResult<PageOf<Asset>>.NotFound();
        }

        return FetchEntityResult<PageOf<Asset>>.Success(result);
    }

    private async Task<bool> DoesBatchExist(T request, CancellationToken cancellationToken)
    {
        var batchExists = await dlcsContext.Batches.AsNoTracking()
            .AnyAsync(b => b.Customer == request.CustomerId && b.Id == request.BatchId, cancellationToken);
        return batchExists;
    }
}
