using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.Repository.Adjuncts;

public static class AdjunctX
{
    public static IQueryable<Adjunct> FindAdjunct(this IQueryable<Adjunct> adjuncts, string adjunctId, AssetId assetId) =>
        adjuncts.Where(a => a.Id == adjunctId && a.AssetId == assetId);

    public static IEnumerable<Adjunct> FindAdjuncts(this IQueryable<Adjunct> adjuncts,
        IDictionary<AssetId, List<string>> adjunctsToFind) =>
        adjuncts.Where(a => adjunctsToFind.Keys.Contains(a.AssetId)).ToList().Where(a =>
            adjunctsToFind.Any(af => af.Key == a.AssetId && af.Value.Contains(a.Id)));
}
