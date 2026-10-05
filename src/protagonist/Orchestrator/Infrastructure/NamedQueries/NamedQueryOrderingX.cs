using System;
using System.Linq;
using System.Linq.Expressions;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using QueryMapping = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.QueryMapping;
using OrderDirection = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.OrderDirection;
using QueryOrder = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.QueryOrder;

namespace Orchestrator.Infrastructure.NamedQueries;

/// <summary>
/// Extension methods for ordering assets as specified by a named query
/// </summary>
public static class NamedQueryOrderingX
{
    /// <summary>
    /// Add named query ordering to queryable of assets.
    /// <see cref="QueryMapping.Unset"/> orderings are ignored - if every ordering is unset the queryable is returned
    /// unaltered
    /// </summary>
    /// <param name="assets">Queryable of assets</param>
    /// <param name="query">Parsed NQ containing appropriate order parameters</param>
    /// <returns>Queryable with ordering applied</returns>
    /// <remarks>
    /// If the queryable is backed by the datastore then ordering is carried out there, rather than in memory
    /// </remarks>
    public static IQueryable<Asset> OrderByNamedQuery(this IQueryable<Asset> assets, ParsedNamedQuery query)
    {
        IOrderedQueryable<Asset>? ordered = null;

        foreach (var queryOrder in query.AssetOrdering.Where(qo => qo.QueryMapping != QueryMapping.Unset))
        {
            ordered = AddOrderBy(assets, ordered, queryOrder);
        }

        return ordered ?? assets;
    }

    /// <summary>
    /// Apply a single ordering to queryable of assets, using ThenBy() if <paramref name="ordered"/> has value
    /// </summary>
    /// <remarks>
    /// The keySelector type differs per mapping, so each is applied via a generic method - a single keySelector
    /// boxing to object can't be translated to SQL
    /// </remarks>
    private static IOrderedQueryable<Asset> AddOrderBy(IQueryable<Asset> assets, IOrderedQueryable<Asset>? ordered,
        QueryOrder queryOrder)
    {
        return queryOrder.QueryMapping switch
        {
            QueryMapping.Number1 => Order(a => a.NumberReference1),
            QueryMapping.Number2 => Order(a => a.NumberReference2),
            QueryMapping.Number3 => Order(a => a.NumberReference3),
            QueryMapping.String1 => Order(a => a.Reference1),
            QueryMapping.String2 => Order(a => a.Reference2),
            QueryMapping.String3 => Order(a => a.Reference3),
            _ => throw new ArgumentOutOfRangeException(nameof(queryOrder), queryOrder.QueryMapping,
                "Unable to order assets by mapping")
        };

        IOrderedQueryable<Asset> Order<T>(Expression<Func<Asset, T>> keySelector)
            => (ordered, queryOrder.OrderDirection) switch
            {
                (null, OrderDirection.Ascending) => assets.OrderBy(keySelector),
                (null, _) => assets.OrderByDescending(keySelector),
                (_, OrderDirection.Ascending) => ordered.ThenBy(keySelector),
                (_, _) => ordered.ThenByDescending(keySelector)
            };
    }
}
