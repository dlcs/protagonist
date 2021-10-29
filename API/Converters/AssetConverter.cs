using System;
using System.Linq;
using DLCS.HydraModel;

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
        public static Image ToHydra(this DLCS.Model.Assets.Asset dbAsset, string baseUrl, string resourceBaseUrl)
        {
            var image = new Image(baseUrl, dbAsset.Customer, dbAsset.Space, dbAsset.Id)
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
        
    }
}