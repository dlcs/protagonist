using System.Collections.Generic;
using System.Linq;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using QueryMapping = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.QueryMapping;
using OrderDirection = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.OrderDirection;
using QueryOrder = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.QueryOrder;

namespace Orchestrator.Infrastructure.NamedQueries;

/// <summary>
/// Utility methods for projecting named queries
/// </summary>
internal static class NamedQueryProjections
{
    /// <summary>
    /// Get assets with correct ordering applied
    /// </summary>
    /// <param name="assets">Collection of assets</param>
    /// <param name="query">Parsed NQ containing appropriate order parameters</param>
    /// <returns>Initial assets ordered appropriately </returns>
    public static IOrderedEnumerable<Asset> GetOrderedAssets(IEnumerable<Asset> assets, ParsedNamedQuery query)
    {
        var assetOrdering = query.AssetOrdering;
        var orderedEnumerable = AddOrderBy(assets, assetOrdering.First());
        
        foreach (var queryOrder in assetOrdering.Skip(1))
        {
            orderedEnumerable = AddOrderBy(orderedEnumerable, queryOrder);
        }

        return orderedEnumerable;
    }
    
    public static object GetOrderingElement(Asset image, QueryMapping queryMapping)
        => queryMapping switch
        {
            QueryMapping.Number1 => image.NumberReference1,
            QueryMapping.Number2 => image.NumberReference2,
            QueryMapping.Number3 => image.NumberReference3,
            QueryMapping.String1 => image.Reference1,
            QueryMapping.String2 => image.Reference2,
            QueryMapping.String3 => image.Reference3,
            _ => 0
        };

    private static IOrderedEnumerable<Asset> AddOrderBy(IEnumerable<Asset> assets, QueryOrder queryOrder)
        => queryOrder.OrderDirection == OrderDirection.Ascending
            ? assets.OrderBy(a => GetOrderingElement(a, queryOrder.QueryMapping))
            : assets.OrderByDescending(a => GetOrderingElement(a, queryOrder.QueryMapping));
    
    private static IOrderedEnumerable<Asset> AddOrderBy(IOrderedEnumerable<Asset> assets, QueryOrder queryOrder)
        => queryOrder.OrderDirection == OrderDirection.Ascending
            ? assets.ThenBy(a => GetOrderingElement(a, queryOrder.QueryMapping))
            : assets.ThenByDescending(a => GetOrderingElement(a, queryOrder.QueryMapping));
}