using System.Diagnostics;
using API.Exceptions;
using DLCS.Core.Collections;
using DLCS.Core.Enum;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Adjunct = DLCS.Model.Assets.Adjunct;

namespace API.Converters;

public static class AdjunctConverter
{
    /// <summary>
    /// This will create a DLCS.Model.Adjunct from a DLCS.HydraModel.Adjunct
    /// </summary>
    /// <param name="hydraAdjunct">The incoming request</param>
    /// <param name="customerId">Required: an assertion of who this Asset belongs to</param>
    /// <param name="spaceId">Required: an assertion of the Space it's in. If not supplied will be determined from Hydra object.</param>
    /// <param name="assetId">Required: The asset id this adjunct is associated with</param>
    /// <param name="adjunctId">
    /// Optional: The potential id of the adjunct.
    /// If not supplied, will be determined from Hydra object.</param>
    /// <returns>A DLCS model representation of the adjunct</returns>
    public static Adjunct ToDlcsModel(this DLCS.HydraModel.Adjunct hydraAdjunct, int customerId, int spaceId,
        string assetId, string? adjunctId = null)
    {
        if (adjunctId.IsNullOrEmpty())
        {
            adjunctId = hydraAdjunct.ModelId;
        }
        
        var enumParsed = Enum.TryParse(hydraAdjunct.IIIFLink, true, out IIIFLinkType iiifLink);

        if (!enumParsed)
        {
            throw new APIException("Hydra adjunct does iiifLink could not be parsed");
        }
        
        Debug.Assert(adjunctId != null, "adjunctId != null");

        var label = hydraAdjunct.Label.ToLanguageMap();

        return new Adjunct
        {
            Id = adjunctId,
            Type = hydraAdjunct.Type,
            MediaType = hydraAdjunct.MediaType!,
            IIIFLink = iiifLink,
            AssetId = new AssetId(customerId, spaceId, assetId),
            Profile = hydraAdjunct.Profile,
            Label = label,
            Language = hydraAdjunct.Language,
            ExternalId = new Uri(hydraAdjunct.ExternalId)
        };
    }
    
    /// <summary>
    /// This will create a DLCS.HydraModel.Adjunct from a DLCS.Model.Adjunct
    /// </summary>
    /// <param name="adjunct">The DLCS adjunct to convert</param>
    /// <param name="urlRoots">The base address used to create FQDN paths</param>
    /// <returns>A hydra model representation of the adjunct</returns>
    public static DLCS.HydraModel.Adjunct ToHydra(this Adjunct adjunct, UrlRoots urlRoots)
    {
        return new DLCS.HydraModel.Adjunct(urlRoots.BaseUrl, adjunct.AssetId.Customer, adjunct.AssetId.Space, adjunct.AssetId.Asset, adjunct.Id)
        {
            Type = adjunct.Type,
            MediaType = adjunct.MediaType,
            IIIFLink = adjunct.IIIFLink.GetDescription(),
            AssetId = adjunct.AssetId.ToString(),
            Profile = adjunct.Profile,
            Label = adjunct.Label,
            Language = adjunct.Language,
            ExternalId = adjunct.ExternalId.ToString(),
            PublicId = adjunct.ExternalId.ToString(),
            Created = adjunct.Created,
            Finished = adjunct.Finished
        };
    }
}
