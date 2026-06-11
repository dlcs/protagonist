using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    /// This will create a DLCS <see cref="DLCS.Model.Assets.Adjunct"/> from a hydra <see cref="DLCS.HydraModel.Adjunct"/>.
    /// The assetId will be determined from the hydra object.
    /// </summary>
    /// <param name="hydraAdjunct">The incoming request</param>
    /// <param name="customerId">Who this Adjunct belongs to</param>
    /// <param name="adjunctId">
    /// The potential id of the adjunct. If not supplied, will be determined from Hydra object.
    /// </param>
    /// <returns>A <see cref="DLCS.Model.Assets.Adjunct"/> representation of the adjunct</returns>
    public static Adjunct ToDlcsModel(this DLCS.HydraModel.Adjunct hydraAdjunct, int customerId, string? adjunctId = null)
    {
        if (!TryParseAssetId(hydraAdjunct.Asset, out var assetId))
        {
            throw new BadRequestException(
                $"Could not parse asset reference '{hydraAdjunct.Asset}' for adjunct '{hydraAdjunct.ModelId}'. " +
                "Use short form 'customer/space/assetId' or a fully qualified URI");
        }
        
        if (assetId.Customer != customerId)
        {
            throw new BadRequestException($"Asset '{assetId}' does not belong to customer {customerId}");
        }
        
        return hydraAdjunct.ToDlcsModel(customerId, assetId.Space, assetId.Asset, adjunctId);
    }
    
    /// <summary>
    /// This will create a DLCS <see cref="DLCS.Model.Assets.Adjunct"/> from a hydra <see cref="DLCS.HydraModel.Adjunct"/>
    /// </summary>
    /// <param name="hydraAdjunct">The incoming request</param>
    /// <param name="customerId">Who this Adjunct belongs to</param>
    /// <param name="spaceId">Space the Adjunct is in</param>
    /// <param name="assetId">The asset id this adjunct is associated with</param>
    /// <param name="adjunctId">
    /// The potential id of the adjunct. If not supplied, will be determined from Hydra object.
    /// </param>
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
            Ingesting = adjunct.Ingesting,
            Batch = adjunct.Batch.HasValue
                ? $"{urlRoots.BaseUrl}/customers/{adjunct.AssetId.Customer}/adjunctQueue/batches/{adjunct.Batch}"
                : null,
        };
    
    /// <summary>
    /// Attempts to parse an asset reference from a short form ("customer/space/assetId") or full URI.
    /// </summary>
    private static bool TryParseAssetId(string? assetField, [NotNullWhen(true)] out AssetId? assetId)
    {
        assetId = null;

        if (string.IsNullOrWhiteSpace(assetField)) return false;

        // Try full URI: extract /customers/{c}/spaces/{s}/images/{i}
        if (Uri.TryCreate(assetField, UriKind.Absolute, out var uri))
        {
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var customersIdx = Array.FindIndex(parts,
                p => p.Equals("customers", StringComparison.OrdinalIgnoreCase));
            if (customersIdx >= 0 && customersIdx + 5 < parts.Length
                                  && parts[customersIdx + 2].Equals("spaces", StringComparison.OrdinalIgnoreCase)
                                  && parts[customersIdx + 4].Equals("images", StringComparison.OrdinalIgnoreCase)
                                  && int.TryParse(parts[customersIdx + 1], out var customer)
                                  && int.TryParse(parts[customersIdx + 3], out var space))
            {
                try
                {
                    assetId = new AssetId(customer, space, parts[customersIdx + 5]);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        // Try short form: customer/space/assetId
        try
        {
            assetId = AssetId.FromString(assetField);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
