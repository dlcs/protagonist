using System;
using DLCS.Core.Strings;
using DLCS.Model.Assets;

namespace API.Features.Image.Requests
{
    public record AssetUpsertValidationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    public class AssetValidator
    {
        /// <summary>
        /// This is essentially the logic in 
        /// https://github.com/digirati-co-uk/deliverator/blob/master/DLCS.Application/Behaviour/API/ValidateImageUpsertBehaviour.cs
        ///
        /// DISCUSS: should this be used for PatchImage too?
        /// </summary>
        /// <param name="existingAsset">If this is an update, the current version of the asset</param>
        /// <param name="updateAsset">The new or updated asset</param>
        /// <returns>A validation result</returns>
        public static AssetUpsertValidationResult ValidateImageUpsert(Asset? existingAsset, Asset updateAsset)
        {
            if (existingAsset != null)
            {
                if (updateAsset.Customer == 0)
                {
                    updateAsset.Customer = existingAsset.Customer;
                }
                if (updateAsset.Space == 0)
                {
                    updateAsset.Space = existingAsset.Space;
                }
            }

            if (updateAsset.Created == DateTime.MinValue)
            {
                if (existingAsset != null && existingAsset.Created != DateTime.MinValue)
                {
                    updateAsset.Created = existingAsset.Created;
                }
                else
                {
                    updateAsset.Created = DateTime.UtcNow;
                }
            }
            
            // TODO:
            // 1. Deliverator has Image.ReservedIds, but it's { } (empty). So will leave out that check.
            // 2. Eventually Protagonist will restrict IDs to url-safe paths, but not for now.
            
            if (existingAsset != null)
            {
                // Don't allow dimensions to be edited:
                // TODO: we could also validate combinations here - w,h | w,h,d | d | <none>
                if (updateAsset.Width != 0 && updateAsset.Width != existingAsset.Width)
                {
                    return new AssetUpsertValidationResult { ErrorMessage = "Width cannot be edited." };
                }
                if (updateAsset.Height != 0 && updateAsset.Height != existingAsset.Height)
                {
                    return new AssetUpsertValidationResult { ErrorMessage = "Height cannot be edited." };
                }
                if (updateAsset.Duration != 0 && updateAsset.Duration != existingAsset.Duration)
                {
                    return new AssetUpsertValidationResult { ErrorMessage = "Duration cannot be edited." };
                }
                
                if (updateAsset.PreservedUri.HasText() && updateAsset.PreservedUri != existingAsset.PreservedUri)
                {
                    return new AssetUpsertValidationResult { ErrorMessage = "PreservedUri cannot be edited." };
                }
                if (updateAsset.Error.HasText() && updateAsset.Error != existingAsset.Error)
                {
                    return new AssetUpsertValidationResult { ErrorMessage = "Error cannot be edited." };
                }
                if (updateAsset.Batch != 0 && updateAsset.Batch != existingAsset.Batch)
                {
                    return new AssetUpsertValidationResult { ErrorMessage = "Batch cannot be edited." };
                }

                if (updateAsset.ImageOptimisationPolicy != existingAsset.ImageOptimisationPolicy)
                {
                    // I think it can be edited though, and doing so would trigger a reingest.
                    return new AssetUpsertValidationResult { ErrorMessage = "ImageOptimisationPolicy cannot be edited." };
                }
                if (updateAsset.ThumbnailPolicy != existingAsset.ThumbnailPolicy)
                {
                    // And this one DEFINITELY should be editable.
                    return new AssetUpsertValidationResult { ErrorMessage = "ThumbnailPolicy cannot be edited." };
                }
                
                if (updateAsset.Family != existingAsset.Family)
                {
                    return new AssetUpsertValidationResult { ErrorMessage = "Family cannot be edited." };
                }

                updateAsset.Finished = existingAsset.Finished;
                updateAsset.Batch = existingAsset.Batch;
                updateAsset.Created = existingAsset.Created;
            }

            return new AssetUpsertValidationResult { Success = true };

        }
        
    }
}