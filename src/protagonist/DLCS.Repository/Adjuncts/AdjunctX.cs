using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using LinqKit;
using LinqKit.Core;

namespace DLCS.Repository.Adjuncts;

public static class AdjunctX
{
    public static IQueryable<Adjunct> FindAdjunct(this IQueryable<Adjunct> adjuncts, string adjunctId, AssetId assetId) =>
        adjuncts.Where(a => a.Id == adjunctId && a.AssetId == assetId);

    public static IQueryable<Adjunct> FindAdjuncts(this IQueryable<Adjunct> adjuncts,
        IDictionary<AssetId, List<string>> adjunctsToFind)
    {
        // Linq cannot directly make this query in SQL, so use the predicate builder instead
        var predicate = PredicateBuilder.New<Adjunct>(false);
        foreach (var (assetId, adjunctIds) in adjunctsToFind)
        {
            predicate = predicate.Or(a => a.AssetId == assetId && adjunctIds.Contains(a.Id));
        }

        return adjuncts.AsExpandable().Where(predicate);
    }
}
