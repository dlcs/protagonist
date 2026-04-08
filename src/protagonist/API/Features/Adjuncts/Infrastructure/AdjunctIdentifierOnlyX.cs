using System.Collections.Generic;
using DLCS.Model;

namespace API.Features.Adjuncts.Infrastructure;

public static class AdjunctIdentifierOnlyX
{
    /// <summary>
    /// Consolidates a list of <see cref="AdjunctIdentifierOnly"/> into a dictionary where the key is the asset id 
    /// </summary>
    public static Dictionary<string, List<string>> ConvertToDictionary(this IEnumerable<AdjunctIdentifierOnly> adjunctIdentifiers)
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

    /// <summary>
    /// Flattens a list of <see cref="AdjunctIdentifierOnly"/> into a serikes of key value pairs of combinations
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> Flatten(this IEnumerable<AdjunctIdentifierOnly> adjunctIdentifier)
    {
        return adjunctIdentifier.SelectMany(a => a.Adjunct.Select(adjunct => new KeyValuePair<string, string>(a.Id, adjunct)));
    }
}
