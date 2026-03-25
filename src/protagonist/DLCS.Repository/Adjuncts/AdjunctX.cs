using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.Repository.Adjuncts;

public static class AdjunctX
{
    public static IQueryable<Adjunct> FindAdjunct(this IQueryable<Adjunct> adjuncts, string adjunctId, AssetId assetId) =>
        adjuncts.Where(a => a.Id == adjunctId && a.AssetId == assetId);
}
