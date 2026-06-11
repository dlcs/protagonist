using System.Collections.Generic;
using API.Exceptions;
using DLCS.Core.Exceptions;
using DLCS.Core.Types;
using DLCS.Model;

namespace API.Features.Customer.Infrastructure;

public static class AdjunctIdentifierOnlyX
{
    /// <summary>
    /// Consolidates a list of <see cref="AdjunctAssetIdentifier"/> into a dictionary where the key is the asset id 
    /// </summary>
    public static Dictionary<AssetId, List<string>> ConvertToDictionary(this IEnumerable<AdjunctAssetIdentifier> adjunctIdentifiers, int customerId)
    {
        var adjunctDictionary = new Dictionary<AssetId, List<string>>();

        try
        {
            foreach (var adjunctIdentifier in adjunctIdentifiers)
            {
                var assetId = AssetId.FromString(adjunctIdentifier.Id);
                
                if (assetId.Customer != customerId)
                {
                    throw new BadRequestException($"Asset id '{assetId}' cannot belong to a different customer");
                }

                if (!adjunctDictionary.TryAdd(assetId, adjunctIdentifier.Adjunct))
                {
                    adjunctDictionary[assetId].AddRange(adjunctIdentifier.Adjunct);
                }
            }
        }
        catch (InvalidAssetIdException assetIdEx)
        {
            throw new BadRequestException(assetIdEx.Message, assetIdEx);
        }
        
        return adjunctDictionary;
    }
}
