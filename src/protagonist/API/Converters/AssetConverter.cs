using API.Exceptions;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using Hydra;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AssetFamily = DLCS.HydraModel.AssetFamily;

namespace API.Converters;

/// <summary>
/// Conversion between API and EF model forms of resources.
/// </summary>
public static class AssetConverter
{
    private static JsonSerializer jsonSerializer = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
    };

    /// <summary>
    /// Converts the EF model object to an API resource.
    /// </summary>
    /// <param name="dbAsset"></param>
    /// <param name="urlRoots">The domain name of the API and orchestrator applications</param>
    /// <param name="includeAdjuncts">True if embedded adjuncts should be included in response</param>
    /// <returns><see cref="Image"/> hydra model</returns>
    public static Image ToHydra(this Asset dbAsset, UrlRoots urlRoots, bool includeAdjuncts = false)
    {
        if (dbAsset.Id.Customer != dbAsset.Customer || dbAsset.Id.Space != dbAsset.Space)
        {
            throw new BadRequestException(
                $"Asset {dbAsset.Id} does not start with expected prefix {dbAsset.Customer}/{dbAsset.Space}/");
        }

        var modelId = dbAsset.Id.Asset;

        var image = new Image(urlRoots.BaseUrl, dbAsset.Customer, dbAsset.Space, modelId)
        {
            ImageService = $"{urlRoots.ResourceRoot}iiif-img/{dbAsset.Id}",
            ThumbnailImageService = dbAsset.HasDeliveryChannel(AssetDeliveryChannels.Thumbnails)
                ? $"{urlRoots.ResourceRoot}thumbs/{dbAsset.Id}"
                : null,
            Created = dbAsset.Created,
            Origin = dbAsset.Origin,
            MaxUnauthorised = dbAsset.MaxUnauthorised,
            MaxWidth = dbAsset.MaxWidth,
            OpenFullMax = dbAsset.OpenFullMax,
            Finished = dbAsset.Finished,
            Ingesting = dbAsset.Ingesting,
            Error = dbAsset.Error,
            Tags = dbAsset.TagsList.ToArray(),
            String1 = dbAsset.Reference1,
            String2 = dbAsset.Reference2,
            String3 = dbAsset.Reference3,
            Number1 = dbAsset.NumberReference1,
            Number2 = dbAsset.NumberReference2,
            Number3 = dbAsset.NumberReference3,
            Duration = dbAsset.Duration,
            Width = dbAsset.Width,
            Height = dbAsset.Height,
            MediaType = dbAsset.MediaType,
            Family = (AssetFamily)dbAsset.Family,
            Roles = dbAsset.RolesList.ToArray(),
            Manifests = dbAsset.Manifests?.ToArray() ?? [],
            Manifest = $"{urlRoots.ResourceRoot}iiif-manifest/{dbAsset.Id}",
        };

        if (dbAsset.Batch > 0)
        {
            // TODO - this should be set by HydraProperty - but where does the template come from?
            image.Batch = $"{urlRoots.BaseUrl}/customers/{dbAsset.Customer}/queue/batches/{dbAsset.Batch}";
        }

        if (!string.IsNullOrEmpty(dbAsset.ThumbnailPolicy))
        {
            image.ThumbnailPolicy = $"{urlRoots.BaseUrl}/thumbnailPolicies/{dbAsset.ThumbnailPolicy}";
        }

        if (!string.IsNullOrEmpty(dbAsset.ImageOptimisationPolicy))
        {
            image.ImageOptimisationPolicy =
                $"{urlRoots.BaseUrl}/imageOptimisationPolicies/{dbAsset.ImageOptimisationPolicy}";
        }
        
        if (!dbAsset.ImageDeliveryChannels.IsNullOrEmpty())
        {
            image.DeliveryChannels = dbAsset.ImageDeliveryChannels.Select(c => new DeliveryChannel
            {
                    Channel = c.Channel,
                    Policy = c.DeliveryChannelPolicy.System
                        ? c.DeliveryChannelPolicy.Name
                        : $"{urlRoots.BaseUrl}/customers/{c.DeliveryChannelPolicy.Customer}/deliveryChannelPolicies/{c.Channel}/{c.DeliveryChannelPolicy.Name}"
                }).ToArray();
        }
        else
        {
            image.DeliveryChannels = [];
        }

        if (includeAdjuncts)
        {
            // If we are including adjuncts - embed child adjuncts as full objects, or set empty array if none
            image.Adjuncts = dbAsset.Adjuncts != null
                ? JToken.FromObject(dbAsset.Adjuncts.Select(a => a.ToHydra(urlRoots)).ToArray(), jsonSerializer)
                : new JArray();
        }

        return image;
    }

    /// <summary>
    /// This will create a <see cref="Asset"/> with the correct Id, Customer and Space.
    /// It will still have null fields, if the incoming Hydra object doesn't supply them.
    ///
    /// So it's not yet ready to be inserted or updated in the DB; it needs further
    /// validation and default settings applied.
    /// </summary>
    /// <param name="hydraImage">The incoming request</param>
    /// <param name="customerId">Required: an assertion of who this Asset belongs to</param>
    /// <param name="spaceId">Optional: an assertion of the Space it's in. If not supplied will be determined from Hydra object.</param>
    /// <param name="modelId">
    /// Optional: an assertion of the Model id component of the Id, as in `customer/space/modelId`.
    /// If not supplied, will be determined from Hydra object.</param>
    /// <returns>The partially populated Asset</returns>
    /// <exception cref="APIException"></exception>
    public static Asset ToDlcsModel(this Image hydraImage, int customerId, int? spaceId = null, string? modelId = null)
    {
        var asset = GetAssetWithIdentifiers(hydraImage, customerId, spaceId, modelId);

        // This conversion should not be supported?
        if (hydraImage.Batch != null)
        {
            asset.Batch = hydraImage.Batch.GetLastPathElementAsInt()!.Value;
        }

        if (hydraImage.Created != null)
        {
            asset.Created = hydraImage.Created.Value.ToUniversalTime();
        }

        if (hydraImage.Duration != null)
        {
            asset.Duration = hydraImage.Duration.Value;
        }

        if (hydraImage.Error != null)
        {
            asset.Error = hydraImage.Error;
        }

        if (hydraImage.Family != null)
        {
            asset.Family = (DLCS.Model.Assets.AssetFamily)hydraImage.Family;
        }

        if (hydraImage.Finished != null)
        {
            asset.Finished = hydraImage.Finished;
        }

        if (hydraImage.Width != null)
        {
            asset.Width = hydraImage.Width.Value;
        }
        if (hydraImage.Height != null)
        {
            asset.Height = hydraImage.Height.Value;
        }

        if (hydraImage.Ingesting != null)
        {
            asset.Ingesting = hydraImage.Ingesting.Value;
        }

        if (hydraImage.Origin != null)
        {
            asset.Origin = hydraImage.Origin;
        }

        if (hydraImage.String1 != null)
        {
            asset.Reference1 = hydraImage.String1;
        }
        if (hydraImage.String2 != null)
        {
            asset.Reference2 = hydraImage.String2;
        }
        if (hydraImage.String3 != null)
        {
            asset.Reference3 = hydraImage.String3;
        }

        if (hydraImage.Number1 != null)
        {
            asset.NumberReference1 = hydraImage.Number1.Value;
        }
        if (hydraImage.Number2 != null)
        {
            asset.NumberReference2 = hydraImage.Number2.Value;
        }
        if (hydraImage.Number3 != null)
        {
            asset.NumberReference3 = hydraImage.Number3.Value;
        }
        
        if (hydraImage.Tags != null)
        {
            asset.TagsList = hydraImage.Tags;
        }

        SetSizeRestriction(hydraImage, asset);

        if (hydraImage.MediaType != null)
        {
            asset.MediaType = hydraImage.MediaType;
        }
        
        var thumbnailPolicy = hydraImage.ThumbnailPolicy.GetLastPathElement("thumbnailPolicies/");
        if (thumbnailPolicy != null)
        {
            asset.ThumbnailPolicy = thumbnailPolicy;
        }
        else if (hydraImage.ThumbnailPolicy.HasText())
        {
            asset.ThumbnailPolicy = hydraImage.ThumbnailPolicy;
        }
        
        var imageOptimisationPolicy = hydraImage.ImageOptimisationPolicy.GetLastPathElement("imageOptimisationPolicies/");
        if (imageOptimisationPolicy != null)
        {
            asset.ImageOptimisationPolicy = imageOptimisationPolicy;
        }
        else if (hydraImage.ImageOptimisationPolicy.HasText())
        {
            asset.ImageOptimisationPolicy = hydraImage.ImageOptimisationPolicy;
        }
        
        asset.Manifests = hydraImage.Manifests?.ToList();
        
        return asset;
    }

    /// <summary>
    /// Safely built a basic <see cref="Asset"/> with Id, Customer and Space set.
    /// </summary>
    /// <exception cref="BadRequestException">Thrown if identity or space cannot be determined</exception>
    private static Asset GetAssetWithIdentifiers(Image hydraImage, int customerId, int? spaceId, string? modelId)
    {
        if (customerId <= 0)
        {
            throw new BadRequestException("Caller must assert which customer this Asset belongs to.");
        }
        
        hydraImage.CustomerId = customerId;

        if (hydraImage.Space.HasValue && spaceId.HasValue && spaceId.Value != hydraImage.Space.Value)
        {
            throw new BadRequestException("Asserted space does not agree with supplied space.");
        }

        if (!hydraImage.Space.HasValue && spaceId.HasValue)
        {
            // Space was not provided in body; take space from route
            hydraImage.Space = spaceId.Value;
        }
        else if (!hydraImage.Space.HasValue)
        {
            throw new BadRequestException("No Space provided for this Asset.");
        }
        else if (hydraImage.Space < 0)
        {
            throw new BadRequestException("Space must be 0 or greater.");
        }
        
        if (!modelId.IsNullOrEmpty() && hydraImage.ModelId.HasText())
        {
            // The route asserts an id and the body also carries one - they must agree.
            // Tolerate the Deliverator-era full form "{customer}/{space}/{id}" in the body.
            var bodyModelId = hydraImage.ModelId;
            var bodyPrefix = $"{hydraImage.CustomerId}/{hydraImage.Space!.Value}/";
            if (bodyModelId.StartsWith(bodyPrefix))
            {
                bodyModelId = bodyModelId.Substring(bodyPrefix.Length);
            }

            if (bodyModelId != modelId)
            {
                throw new BadRequestException("The id in the request body does not agree with the request URL.");
            }
        }

        if (modelId.IsNullOrEmpty())
        {
            modelId = hydraImage.ModelId;
        }

        if (modelId.IsNullOrEmpty() && hydraImage.Id.HasText())
        {
            modelId = hydraImage.Id.GetLastPathElement();
        }

        if (!modelId.HasText())
        {
            throw new BadRequestException("Hydra Image does not have a ModelId, and no ModelId could be inferred.");
        }
        
        // This is a silent test for backwards compatibility with Deliverator.
        // DDS sends Patch ModelIDs in full ID form:
        var testPrefix = $"{hydraImage.CustomerId}/{hydraImage.Space.Value}/";
        if (modelId.StartsWith(testPrefix))
        {
            modelId = modelId.Substring(testPrefix.Length);
        }

        var assetId = GetValidAssetId(hydraImage, modelId);
        var asset = new Asset
        {
            Id = assetId,
            Customer = hydraImage.CustomerId,
            Space = hydraImage.Space.Value
        };
        return asset;
    }
    
    private static AssetId GetValidAssetId(Image hydraImage, string modelId)
    {
        var assetId = new AssetId(hydraImage.CustomerId, hydraImage.Space!.Value, modelId);
        if (!hydraImage.Id.HasText()) return assetId;

        var idParts = hydraImage.Id.Split("/");
        if (idParts.Length < 6)
        {
            throw new BadRequestException($"Caller supplied an ID that is not in the correct form ({hydraImage.Id})");
        }
        idParts = idParts[^6..];
        if (idParts[0] != "customers" || idParts[2] != "spaces" || idParts[4] != "images")
        {
            throw new BadRequestException($"Caller supplied an ID that is not in the correct form ({hydraImage.Id})");
        }

        var assetIdFromHydraId = AssetId.FromString($"{idParts[1]}/{idParts[3]}/{idParts[5]}");
        if (assetIdFromHydraId != assetId)
        {
            throw new BadRequestException("Caller supplied an ID that is not supported by the request URL");
        }
        // it's OK if the caller didn't explicitly provide an Id in the JSON body - but it's an error
        // if they supply one that disagrees with the assertions provided in the method call.
        // e.g., used for PUT to a URL, the route params are passed to this method.

        return assetId;
    }
    
    /// <summary>
    /// Set fields that can control access restrictions: maxWidth, openFullMax and roles
    /// Also handles maxUnauthorised value, which is deprecated but will be supported for a short period 
    /// </summary>
    private static void SetSizeRestriction(Image hydraImage, Asset targetAsset)
    {
        // maxUnauthorised is superseded but we support mapping from this to new values for a period
        if (hydraImage.MaxUnauthorised != null)
        {
            // Save the provided value, it won't be used in any logic but is a marker that we received a val
            var maxUnauth = hydraImage.MaxUnauthorised.Value;
            targetAsset.MaxUnauthorised = maxUnauth;
            
            if (!hydraImage.Roles.IsNullOrEmpty())
            {
                // If roles have been provided, use them
                targetAsset.RolesList = hydraImage.Roles!;
            }
            else
            {
                // No roles provided but we may need to assign an unobtainable role to simulate behaviour
                if (maxUnauth >= 0)
                {
                    targetAsset.RolesList = [Asset.UnobtainableRole];
                }
            }
            
            targetAsset.OpenFullMax = GetUsableValue(maxUnauth);
            return;
        }
        
        if (hydraImage.Roles != null)
        {
            targetAsset.RolesList = hydraImage.Roles;
        }
        
        if (hydraImage.MaxWidth != null)
        {
            targetAsset.MaxWidth = GetUsableValue(hydraImage.MaxWidth.Value);
        }
        
        if (hydraImage.OpenFullMax != null)
        {
            targetAsset.OpenFullMax = GetUsableValue(hydraImage.OpenFullMax.Value);
        }

        return;
        
        // A value of <= 0 is 'unset', meaning we store 0
        int GetUsableValue(int providedValue) => Math.Max(0, providedValue);
    }
}
