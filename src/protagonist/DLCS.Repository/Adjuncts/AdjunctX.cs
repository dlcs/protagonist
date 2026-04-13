using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using LinqKit;

namespace DLCS.Repository.Adjuncts;

public static class AdjunctX
{
    /// <summary>
    /// Generate <see cref="IQueryable"/> for finding an individual adjunct
    /// </summary>
    public static IQueryable<Adjunct> FindAdjunct(this IQueryable<Adjunct> adjuncts, string adjunctId, AssetId assetId) =>
        adjuncts.Where(a => a.Id == adjunctId && a.AssetId == assetId);

    /// <summary>
    /// Generate <see cref="IQueryable"/> for finding adjuncts based on a set of adjuncts to search for
    /// </summary>
    public static IQueryable<Adjunct> FindAdjuncts(this IQueryable<Adjunct> adjuncts,
        IDictionary<AssetId, List<string>> adjunctsToFind)
        => adjuncts.FindAdjunctsInternal(adjunctsToFind.Select(kvp => (kvp.Key, (IEnumerable<string>)kvp.Value)));

    /// <summary>
    /// Generate <see cref="IQueryable"/> for finding adjuncts based on a set of adjuncts to search for
    /// </summary>
    public static IQueryable<Adjunct> FindAdjuncts(this IQueryable<Adjunct> adjuncts,
        ILookup<AssetId, string> adjunctsToFind)
        => adjuncts.FindAdjunctsInternal(adjunctsToFind.Select(g => (g.Key, g.Select(ids => ids))));

    // Linq cannot directly make this query in SQL, so use the predicate builder instead
    private static IQueryable<Adjunct> FindAdjunctsInternal(this IQueryable<Adjunct> adjuncts,
        IEnumerable<(AssetId assetId, IEnumerable<string> adjunctIds)> pairs)
    {
        var predicate = PredicateBuilder.New<Adjunct>(false);
        foreach (var (assetId, adjunctIds) in pairs)
        {
            predicate = predicate.Or(a => a.AssetId == assetId && adjunctIds.Contains(a.Id));
        }

        return adjuncts.Where(predicate);
    }
}
