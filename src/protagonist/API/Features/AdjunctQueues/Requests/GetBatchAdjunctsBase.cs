using API.Infrastructure.Page;
using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.AdjunctQueues.Requests;

public abstract class GetBatchAdjunctsBase<T>(DlcsContext dlcsContext)
    : IRequestHandler<T, FetchEntityResult<PageOf<Adjunct>>>
    where T : GetBatchAdjuncts
{
    protected abstract IQueryable<Adjunct> GetAdjuncts(DlcsContext dlcsContext, T request);

    public async Task<FetchEntityResult<PageOf<Adjunct>>> Handle(
        T request, CancellationToken cancellationToken)
    {
        var result = await GetAdjuncts(dlcsContext, request).CreatePagedResult(
            request,
            q => q,
            adjuncts => adjuncts.AsOrderedAdjunctQuery(request),
            cancellationToken);

        // Any empty result set could be the result of an empty batch - check if batch exists
        if (result.Total == 0 && !await DoesBatchExist(request, cancellationToken))
        {
            return FetchEntityResult<PageOf<Adjunct>>.NotFound();
        }

        return FetchEntityResult<PageOf<Adjunct>>.Success(result);
    }

    private async Task<bool> DoesBatchExist(T request, CancellationToken cancellationToken)
    {
        var batchExists = await dlcsContext.AdjunctBatches.AsNoTracking()
            .AnyAsync(b => b.Customer == request.CustomerId && b.Id == request.BatchId, cancellationToken);
        return batchExists;
    }
}
