using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Infrastructure.Page;

/// <summary>
/// Shared handler for MediatR requests that fetch a page of entities belonging to a batch (e.g. images/assets in
/// a Batch, or adjuncts in an AdjunctBatch). Runs the paged query and, if it comes back empty, separately checks
/// whether the batch itself exists - this distinguishes "batch not found" (404) from "batch exists but is empty"
/// (200, empty page).
/// </summary>
public abstract class GetBatchEntitiesBase<TEntity, TRequest>(DlcsContext dlcsContext)
    : IRequestHandler<TRequest, FetchEntityResult<PageOf<TEntity>>>
    where TEntity : class, IDeliverable
    where TRequest : IRequest<FetchEntityResult<PageOf<TEntity>>>, IPagedRequest, IOrderableRequest
{
    /// <summary>
    /// The unpaged, unfiltered, unordered query for entities belonging to the batch in <paramref name="request"/>
    /// </summary>
    protected abstract IQueryable<TEntity> GetEntities(DlcsContext dlcsContext, TRequest request);

    /// <summary>
    /// Apply any request-specific filtering. Defaults to no filtering - override for resources that support one
    /// (e.g. the asset ?q= filter)
    /// </summary>
    protected virtual IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> query, TRequest request) => query;

    /// <summary>
    /// Apply ordering to the (already filtered) query, per the request's orderBy/orderByDescending
    /// </summary>
    protected abstract IQueryable<TEntity> ApplyOrdering(IQueryable<TEntity> query, TRequest request);

    /// <summary>
    /// Check whether the batch referenced by <paramref name="request"/> exists, for the requesting customer
    /// </summary>
    protected abstract Task<bool> DoesBatchExist(DlcsContext dlcsContext, TRequest request,
        CancellationToken cancellationToken);

    public async Task<FetchEntityResult<PageOf<TEntity>>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var result = await GetEntities(dlcsContext, request).CreatePagedResult(
            request,
            query => ApplyFilter(query, request),
            query => ApplyOrdering(query, request),
            cancellationToken);

        // Any empty result set could be the result of an applied filter - check if batch exists
        if (result.Total == 0 && !await DoesBatchExist(dlcsContext, request, cancellationToken))
        {
            return FetchEntityResult<PageOf<TEntity>>.NotFound();
        }

        return FetchEntityResult<PageOf<TEntity>>.Success(result);
    }
}
