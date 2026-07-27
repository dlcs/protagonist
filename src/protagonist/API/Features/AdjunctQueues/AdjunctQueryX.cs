using API.Infrastructure.Requests;
using DLCS.Model.Assets;

namespace API.Features.AdjunctQueues;

/// <summary>
/// Extension methods for adjunct queries.
/// </summary>
public static class AdjunctQueryX
{
    /// <summary>
    /// Order adjuncts by Created date - currently the only supported orderBy field for adjuncts.
    /// Any other/unrecognised orderBy value is silently ignored and Created is used.
    /// </summary>
    public static IQueryable<Adjunct> AsOrderedAdjunctQuery(this IQueryable<Adjunct> query, IOrderableRequest orderable)
        => orderable.Descending
            ? query.OrderByDescending(a => a.Created)
            : query.OrderBy(a => a.Created);
}
