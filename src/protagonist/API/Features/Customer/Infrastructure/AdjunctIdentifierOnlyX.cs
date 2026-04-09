using System.Collections.Generic;
using DLCS.Model;

namespace API.Features.Customer.Infrastructure;

public static class AdjunctIdentifierOnlyX
{
    /// <summary>
    /// Consolidates a list of <see cref="AdjunctAssetIdentifier"/> into a dictionary where the key is the asset id 
    /// </summary>
    public static Dictionary<string, List<string>> ConvertToDictionary(this IEnumerable<AdjunctAssetIdentifier> adjunctIdentifiers)
    {
        var adjunctDictionary = new Dictionary<string, List<string>>();
        
        foreach (var adjunctIdentifier in adjunctIdentifiers)
        {
            if (!adjunctDictionary.TryAdd(adjunctIdentifier.Id, adjunctIdentifier.Adjunct))
            {
                adjunctDictionary[adjunctIdentifier.Id].AddRange(adjunctIdentifier.Adjunct);
            }
        }
        
        return adjunctDictionary;
    }
}
