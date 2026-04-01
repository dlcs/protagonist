using System.Collections.Generic;
using DLCS.Core.Types;
using DLCS.Model;

namespace API.Features.Adjuncts.Infrastructure;

public static class AdjunctIdentifierOnlyX
{
    public static IEnumerable<KeyValuePair<AssetId, string>> Flatten(this AdjunctIdentifierOnly adjunctIdentifier)
    {
        return adjunctIdentifier.Adjunct.Select(adjunct => new KeyValuePair<AssetId, string>(adjunctIdentifier.Id, adjunct));
    }
}
