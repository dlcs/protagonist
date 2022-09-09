using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Page;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository;

public static class PagingHelpers
{
    /// <summary>
    /// Create a <see cref="PageOf{T}"/> using specified filter over <see cref="DbSet{T}"/>
    /// </summary>
    /// <param name="entities">Dbset to derive results from</param>
    /// <param name="filter">Filter to apply - this filter is applied to calculate both Total and Entities</param>
    /// <param name="request"><see cref="IPagedRequest"/> containing page and pagesize props</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <typeparam name="T">Type of entity being paged</typeparam>
    /// <returns>
    /// New <see cref="PageOf{T}"/> containing Page, Total result, collection of entities for page and requested page
    /// size
    /// </returns>
    public static async Task<PageOf<T>> CreatePagedResult<T>(this DbSet<T> entities, Expression<Func<T, bool>> filter,
        IPagedRequest request, CancellationToken cancellationToken) where T : class
    {
        var result = new PageOf<T>
        {
            Page = request.Page,
            Total = await entities
                .CountAsync(filter, cancellationToken: cancellationToken),
            Entities = await entities
                .Where(filter)
                .WithPaging(request)
                .ToListAsync(cancellationToken),
            PageSize = request.PageSize
        };

        return result;
    }

    /// <summary>
    /// Apply Skip/Take logic on IQueryable using paged request
    /// </summary>
    public static IQueryable<T> WithPaging<T>(this IQueryable<T> query, IPagedRequest request)
        => query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize);
}