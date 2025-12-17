using System.Diagnostics;
using API.Exceptions;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace API.Converters;

public static class AdjunctConverter
{
    /// <summary>
    /// This will create a DLCS.Model.Assets.Asset with the correct Id, Customer and Space.
    /// It will still have null fields, if the incoming Hydra object doesn't supply them.
    /// 
    /// So it's not yet ready to be inserted or updated in the DB; it needs further
    /// validation and default settings applied.
    /// </summary>
    /// <param name="hydraAdjunct">The incoming request</param>
    /// <param name="customerId">Required: an assertion of who this Asset belongs to</param>
    /// <param name="spaceId">Required: an assertion of the Space it's in. If not supplied will be determined from Hydra object.</param>
    /// <param name="assetId">Required: The asset id this adjunct is associated with</param>
    /// <param name="adjunctId">
    /// Optional: an assertion of the Model id component of the Id, as in `customer/space/modelId`.
    /// If not supplied, will be determined from Hydra object.</param>
    /// <returns>The partially populated Asset</returns>
    public static Adjunct ToDlcsModel(this DLCS.HydraModel.Adjunct hydraAdjunct, int customerId, int spaceId,
        string assetId, string? adjunctId = null)
    {
        if (adjunctId.IsNullOrEmpty())
        {
            adjunctId = hydraAdjunct.ModelId;
        }
        
        var enumParsed = Enum.TryParse(hydraAdjunct.IIIFLink, out IIIFLinkType iiifLink);

        if (!enumParsed)
        {
            throw new APIException("Hydra adjunct does iiifLink could not be parsed");
        }
        
        Debug.Assert(adjunctId != null, "adjunctId != null");

        return new Adjunct
        {
            Id = adjunctId,
            Type = hydraAdjunct.Type,
            MediaType = hydraAdjunct.MediaType!,
            IIIFLink = iiifLink,
            AssetId = new AssetId(customerId, spaceId, assetId),
            Profile = hydraAdjunct.Profile,
            Label = hydraAdjunct.Label,
            Language = hydraAdjunct.Language,
            ExternalId = hydraAdjunct.ExternalId != null ? new Uri(hydraAdjunct.ExternalId) : null
        };
    }
    
    public static DLCS.HydraModel.Adjunct ToHydra(this Adjunct adjunct, UrlRoots urlRoots)
    {
        return new DLCS.HydraModel.Adjunct(urlRoots.BaseUrl, adjunct.AssetId.Customer, adjunct.AssetId.Space, adjunct.AssetId.Asset, adjunct.Id)
        {
            Type = adjunct.Type,
            MediaType = adjunct.MediaType,
            IIIFLink = adjunct.IIIFLink.ToString(),
            AssetId = adjunct.AssetId.ToString(),
            Profile = adjunct.Profile,
            Label = adjunct.Label,
            Language = adjunct.Language,
            ExternalId = adjunct.ExternalId != null ? adjunct.ExternalId.ToString() : null,
            Created = adjunct.Created,
            Modified = adjunct.Modified
        };
    }
}
