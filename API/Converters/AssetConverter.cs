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
        /// <param name="baseUrl"></param>
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

        public static Asset ToDlcsModel(this Image hydraImage)
        {
            string? modelId = hydraImage.ModelId;
            if (!modelId.HasText())
            {
                modelId = hydraImage.Id.GetLastPathElement();
            }

            var assetId = new AssetId(hydraImage.CustomerId, hydraImage.Space, modelId);
            var asset = new Asset
            {
                Id = assetId.ToString(),
                Customer = hydraImage.CustomerId,
                Space = hydraImage.Space
            };
            
            if (hydraImage.Batch != null)
            {
                asset.Batch = hydraImage.Batch.GetLastPathElementAsInt()!.Value;
            }

            if (hydraImage.Created != null)
            {
                asset.Created = hydraImage.Created.Value;
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