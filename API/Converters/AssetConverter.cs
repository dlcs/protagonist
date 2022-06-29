using System;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using Hydra;
using AssetFamily = DLCS.HydraModel.AssetFamily;

namespace API.Converters
{
    /// <summary>
    /// Conversion between API and EF model forms of resources.
    /// </summary>
    public static class AssetConverter
    {
        /// <summary>
        /// Converts the EF model object to an API resource.
        /// </summary>
        /// <param name="dbAsset"></param>
        /// <param name="baseUrl">The API base URL</param>
        /// <param name="resourceBaseUrl">The base URI for image services and other public-facing resources</param>
        /// <returns></returns>
        public static Image ToHydra(this Asset dbAsset, string baseUrl, string resourceBaseUrl)
        {
            // This seems fragile
            // The database Id/PK is {customer}/{space}/{modelId}
            // so we need to remove that bit
            // we can do this in a checking kind of way though:
            var prefix = $"{dbAsset.Customer}/{dbAsset.Space}/";
            if (!dbAsset.Id.StartsWith(prefix))
            {
                throw new APIException($"Asset {dbAsset.Id} does not start with expected prefix {prefix}");
            }

            var modelId = dbAsset.Id.Substring(prefix.Length);
            
            var image = new Image(baseUrl, dbAsset.Customer, dbAsset.Space, modelId)
            {
                InfoJson = $"{resourceBaseUrl}iiif-img/{dbAsset.Id}",
                ThumbnailInfoJson = $"{resourceBaseUrl}thumbs/{dbAsset.Id}",
                Created = dbAsset.Created,
                Origin = dbAsset.Origin,
                InitialOrigin = dbAsset.InitialOrigin,
                MaxUnauthorised = dbAsset.MaxUnauthorised,
                // Queued     -- not directly available on just a dbAsset, needs more info
                // Dequeued
                Finished = dbAsset.Finished,
                Ingesting = dbAsset.Ingesting,
                Error = dbAsset.Error,
                Tags = dbAsset.Tags.Split(",", StringSplitOptions.RemoveEmptyEntries).ToArray(), // TODO - add a hasconversion?,
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
                // Text (to replace with https://github.com/dlcs/protagonist/issues/148)
                // TextType
                Roles = dbAsset.Roles.Split(",", StringSplitOptions.RemoveEmptyEntries).ToArray(), // TODO - add a hasconversion?,
                
                
            };
            if (dbAsset.Batch > 0)
            {
                // TODO - this should be set by HydraProperty - but where does the template come from?
                image.Batch = $"{baseUrl}/customers/{dbAsset.Customer}/queue/batches/{dbAsset.Batch}";
            }
            return image;
        }

        public static Asset ToDlcsModel(this Image hydraImage, int customerId, int? spaceId = null)
        {
            if (customerId <= 0)
            {
                throw new APIException("Caller must assert which customer this Asset belongs to.");
            }
            
            hydraImage.CustomerId = customerId;

            if (hydraImage.Space > 0 && spaceId.HasValue && spaceId.Value != hydraImage.Space)
            {
                throw new APIException("Asserted space does not agree with supplied space.");
            }
            if (hydraImage.Space <= 0)
            {
                if (spaceId.HasValue)
                {
                    hydraImage.Space = spaceId.Value;
                }
                else
                {
                    throw new APIException("No Space provided for this Asset.");
                }
            }
            string? modelId = hydraImage.ModelId;
            if (!modelId.HasText() && hydraImage.Id.HasText())
            {
                modelId = hydraImage.Id.GetLastPathElement();
            }

            if (!modelId.HasText())
            {
                throw new APIException("Hydra Image does not have a ModelId, and no ModelId could be inferred.");
            }
            
            // This is a silent test for backwards compatibility with Deliverator.
            // DDS sends Patch ModelIDs in full ID form:
            // TODO: Wrap this in a feature flag
            var testPrefix = $"{hydraImage.CustomerId}/{hydraImage.Space}/";
            if (modelId.StartsWith(testPrefix))
            {
                modelId = modelId.Substring(testPrefix.Length);
            }

            var assetId = new AssetId(hydraImage.CustomerId, hydraImage.Space, modelId).ToString();
            if (hydraImage.Id.HasText())
            {
                var idParts = hydraImage.Id.Split("/");
                if (idParts.Length < 6)
                {
                    throw new APIException("Caller supplied an ID that is in the correct form");
                }
                idParts = idParts[^6..];
                if (idParts[0] != "customers" || idParts[2] != "spaces" || idParts[4] != "images")
                {
                    throw new APIException("Caller supplied an ID that is in the correct form");
                }
                var assetIdFromHydraId = $"{idParts[1]}/{idParts[3]}/{idParts[5]}";
                if (assetIdFromHydraId != assetId)
                {
                    throw new APIException("Caller supplied an ID that is not supported by the request URL");
                }
                // it's OK if the caller didn't explicitly provide an Id in the JSON body
            }
            var asset = new Asset
            {
                Id = assetId,
                Customer = hydraImage.CustomerId,
                Space = hydraImage.Space
            };
            
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

            if (hydraImage.Roles != null)
            {
                asset.Roles = string.Join(",", hydraImage.Roles);
            }
            if (hydraImage.Tags != null)
            {
                asset.Tags = string.Join(",", hydraImage.Tags);
            }

            if (hydraImage.MaxUnauthorised != null)
            {
                asset.MaxUnauthorised = hydraImage.MaxUnauthorised.Value;
            }

            if (hydraImage.MediaType != null)
            {
                asset.MediaType = hydraImage.MediaType;
            }

            var thumbnailPolicy = hydraImage.ThumbnailPolicy.GetLastPathElement("thumbnailPolicies/");
            if (thumbnailPolicy != null)
            {
                asset.ThumbnailPolicy = thumbnailPolicy;
            }
            
            var imageOptimisationPolicy = hydraImage.ImageOptimisationPolicy.GetLastPathElement("imageOptimisationPolicies/");
            if (imageOptimisationPolicy != null)
            {
                asset.ImageOptimisationPolicy = imageOptimisationPolicy;
            }
            
            // Not patchable?
            // if (hydraImage.InitialOrigin != null)
            // {
            //     asset.InitialOrigin = hydraImage.InitialOrigin;
            // }

            return asset;
        }
        
    }
}