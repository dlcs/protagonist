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
    /// This will create a DLCS <see cref="DLCS.Model.Assets.Adjunct"/> from a hydra <see cref="DLCS.HydraModel.Adjunct"/>
    /// </summary>
    /// <param name="hydraAdjunct">The incoming request</param>
    /// <param name="customerId">Required: an assertion of who this Asset belongs to</param>
    /// <param name="spaceId">Required: an assertion of the Space it's in. If not supplied will be determined from Hydra object.</param>
    /// <param name="assetId">Required: The asset id this adjunct is associated with</param>
    /// <param name="adjunctId">
    /// Optional: The potential id of the adjunct.
    /// If not supplied, will be determined from Hydra object.</param>
    /// <returns>A <see cref="DLCS.Model.Assets.Adjunct"/> representation of the adjunct</returns>
    public static Adjunct ToDlcsModel(this DLCS.HydraModel.Adjunct hydraAdjunct, int customerId, int spaceId,
        string assetId, string? adjunctId = null)
    {
        if (adjunctId.IsNullOrEmpty())
        {
            adjunctId = hydraAdjunct.ModelId;
        }

        if (!Enum.TryParse(hydraAdjunct.IIIFLink, true, out IIIFLinkType iiifLink))
        {
            throw new APIException("Hydra adjunct 'iiifLink' could not be parsed");
        }

        Debug.Assert(adjunctId != null, "adjunctId != null");

        var dlcsAdjunct = new Adjunct
        {
            Id = adjunctId,
            Type = hydraAdjunct.Type!,
            MediaType = hydraAdjunct.MediaType!,
            IIIFLink = iiifLink,
            AssetId = new AssetId(customerId, spaceId, assetId),
            Profile = hydraAdjunct.Profile,
            Label =  hydraAdjunct.Label.ToLanguageMap(),
            Language = hydraAdjunct.Language,
            Size = hydraAdjunct.Size,
            Motivation = hydraAdjunct.Motivation,
            Provides = hydraAdjunct.Provides,
        };
        
        if (hydraAdjunct.Origin is not null)
        {
            dlcsAdjunct.Origin = hydraAdjunct.Origin;
            dlcsAdjunct.SetFieldsForIngestion();
        }
        else
        {
            // by validation: if Origin is null, then ExternalId MUST not be
            dlcsAdjunct.ExternalId = new Uri(hydraAdjunct.ExternalId!);
        }
        
        return dlcsAdjunct;
    }
    
    /// <summary>
    /// This will create a hydra <see cref="DLCS.HydraModel.Adjunct"/> from a DLCS <see cref="DLCS.Model.Assets.Adjunct"/> 
    /// </summary>
    /// <param name="adjunct">The DLCS adjunct to convert</param>
    /// <param name="urlRoots">The base address used to create FQDN paths</param>
    /// <returns>A hydra <see cref="DLCS.HydraModel.Adjunct"/> representation of the adjunct</returns>
    public static DLCS.HydraModel.Adjunct ToHydra(this Adjunct adjunct, UrlRoots urlRoots)
        => new(urlRoots.BaseUrl, adjunct.AssetId.Customer, adjunct.AssetId.Space, adjunct.AssetId.Asset, adjunct.Id)
        {
            Type = adjunct.Type,
            MediaType = adjunct.MediaType,
            IIIFLink = adjunct.IIIFLink.GetDescription(),
            Profile = adjunct.Profile,
            Label = adjunct.Label,
            Language = adjunct.Language,
            Asset = $"{urlRoots.BaseUrl}/customers/{adjunct.AssetId.Customer}/spaces/{adjunct.AssetId.Space}/images/{adjunct.AssetId.Asset}",
            ExternalId = adjunct.ExternalId?.ToString(),
            Origin = adjunct.Origin,
            PublicId = adjunct.ExternalId?.ToString() ??  $"{urlRoots.ResourceRoot}adjuncts/{adjunct.AssetId.Customer}/{adjunct.AssetId.Space}/{adjunct.AssetId.Asset}/{adjunct.Id}",
            Created = adjunct.Created,
            Finished = adjunct.Finished,
            Size = adjunct.Size,
            Error = adjunct.Error,
            Motivation = adjunct.Motivation,
            Provides =  adjunct.Provides,
            Ingesting = adjunct.Ingesting
            Batch = adjunct.Batch.HasValue
                ? $"{urlRoots.BaseUrl}/customers/{adjunct.AssetId.Customer}/adjunctQueue/batches/{adjunct.Batch}"
                : null
        };
}
