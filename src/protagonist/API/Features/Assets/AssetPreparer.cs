using System;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Assets;

namespace API.Features.Assets;

/// <summary>
/// Conveys whether an attempt to prepare an asset for upsert encountered an invalid state.
/// </summary>
public record AssetPreparationResult
{
    /// <summary>
    /// The asset is OK to go to the database
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// The asset cannot go to the DB for this reason.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// As aspect of the Asset has changed that means it needs to be re-processed by Engine.
    /// </summary>
    public bool RequiresReingest { get; set; }
}
    
/// <summary>
/// Prepares an asset for insert or update operations.
/// </summary>
public static class AssetPreparer
{
    private static readonly Asset DefaultAsset;

    /// <summary>
    /// Prepares an asset for insert or update operations.
    /// Validates that things aren't being modified that shouldn't be,
    /// and that no null fields remain on the asset being upserted.
    /// </summary>
    /// <remarks>
    /// This is essentially the logic in 
    /// https://github.com/digirati-co-uk/deliverator/blob/master/DLCS.Application/Behaviour/API/ValidateImageUpsertBehaviour.cs
    /// This method was called ValidateImageUpsert to match deliverator; now renamed
    /// </remarks>
    /// <param name="existingAsset">If this is an update, the current version of the asset</param>
    /// <param name="updateAsset">The new or updated asset</param>
    /// <param name="allowNonApiUpdates">Permit setting of fields that would not be allowed on API calls</param>
    /// <returns>A validation result</returns>
    public static AssetPreparationResult PrepareAssetForUpsert(
        Asset? existingAsset,
        Asset updateAsset,
        bool allowNonApiUpdates)
    {
        if (existingAsset is { NotForDelivery: true })
        {
            // We can relax this later but for now, you cannot use the API
            // to modify an asset marked NotForDelivery.
            return new AssetPreparationResult { ErrorMessage = "Cannot use API to modify a NotForDelivery asset." };
            // However, this DOES allow the *creation* of a NotForDelivery asset.
        }
        
        bool requiresReingest = existingAsset == null;

        if (allowNonApiUpdates == false)
        {
            // These cannot be created or modified via the API
            if (updateAsset.Finished.HasValue)
            {
                return new AssetPreparationResult { ErrorMessage = "Cannot set Finished timestamp via API." };
            }
            
            if (updateAsset.Error != null)
            {
                return new AssetPreparationResult { ErrorMessage = "Cannot set Error state via API." };
            }
        }
        
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

        if (updateAsset.Created == null || updateAsset.Created == DateTime.MinValue)
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
            
        if (existingAsset != null && allowNonApiUpdates == false)
        {
            // https://github.com/dlcs/protagonist/issues/341 for further changes to this validation
            if (updateAsset.Width.HasValue && updateAsset.Width != 0 && updateAsset.Width != existingAsset.Width)
            {
                return new AssetPreparationResult { ErrorMessage = "Width cannot be edited." };
            }
            
            if (updateAsset.Height.HasValue && updateAsset.Height != 0 && updateAsset.Height != existingAsset.Height)
            {
                return new AssetPreparationResult { ErrorMessage = "Height cannot be edited." };
            }
            
            if (updateAsset.Duration.HasValue && updateAsset.Duration != 0 && updateAsset.Duration != existingAsset.Duration)
            {
                return new AssetPreparationResult { ErrorMessage = "Duration cannot be edited." };
            }
                
            if (updateAsset.PreservedUri != null && updateAsset.PreservedUri != existingAsset.PreservedUri)
            {
                return new AssetPreparationResult { ErrorMessage = "PreservedUri cannot be edited." };
            }
            
            if (updateAsset.Error != null && updateAsset.Error != existingAsset.Error)
            {
                return new AssetPreparationResult { ErrorMessage = "Error cannot be edited." };
            }
            
            if (updateAsset.Batch.HasValue && updateAsset.Batch != 0 && updateAsset.Batch != existingAsset.Batch)
            {
                return new AssetPreparationResult { ErrorMessage = "Batch cannot be edited." };
            }

            if (updateAsset.ImageOptimisationPolicy != null && updateAsset.ImageOptimisationPolicy != existingAsset.ImageOptimisationPolicy)
            {
                // I think it should be editable though, and doing so should trigger a re-ingest.
                return new AssetPreparationResult { ErrorMessage = "ImageOptimisationPolicy cannot be edited." };
            }
            
            if (updateAsset.ThumbnailPolicy != null && updateAsset.ThumbnailPolicy != existingAsset.ThumbnailPolicy)
            {
                // And this one DEFINITELY should be editable!
                return new AssetPreparationResult { ErrorMessage = "ThumbnailPolicy cannot be edited." };
            }
                
            if (updateAsset.Family != null && updateAsset.Family != existingAsset.Family)
            {
                return new AssetPreparationResult { ErrorMessage = "Family cannot be edited." };
            }

            if (updateAsset.InitialOrigin != null)
            {
                return new AssetPreparationResult { ErrorMessage = "Cannot edit the InitialOrigin of an asset." };
            }
        }

        if (existingAsset != null)
        {
            if (updateAsset.Origin.HasText() && updateAsset.Origin != existingAsset.Origin)
            {
                requiresReingest = true;
            }
            if (updateAsset.ThumbnailPolicy.HasText() && updateAsset.ThumbnailPolicy != existingAsset.ThumbnailPolicy)
            {
                // requiresReingest = true; NO, because we'll re-create thumbs on demand - "backfill"
                // However, we can treat a PUT as always triggering reingest, whereas a PATCH does not,
                // even if they are otherwise equivalent - see PutOrPatchImage
            }
            if (updateAsset.ImageOptimisationPolicy.HasText() && updateAsset.ImageOptimisationPolicy != existingAsset.ImageOptimisationPolicy)
            {
                requiresReingest = true; // YES, because we've changed the way this image should be processed
            }
        }

        SetNullFieldsToExistingOrDefaults(existingAsset, updateAsset);
        // updateAsset is now ready to be upserted into the database
        
        if (requiresReingest && updateAsset.Origin.IsNullOrEmpty())
        {
            return new AssetPreparationResult { ErrorMessage = "Asset Origin must be supplied." };
        }
    
        return new AssetPreparationResult
        {
            Success = true,
            RequiresReingest = requiresReingest
        };
    }

    /// <summary>
    /// Ensure that any unset fields are given their default Asset value.
    /// </summary>
    /// <param name="templateAsset"></param>
    /// <param name="upsertAsset"></param>
    private static void SetNullFieldsToExistingOrDefaults(Asset? templateAsset, Asset upsertAsset)
    {
        templateAsset ??= DefaultAsset;
            
        // set if null
        upsertAsset.Origin ??= templateAsset.Origin;
        upsertAsset.Tags ??= templateAsset.Tags;
        upsertAsset.Roles ??= templateAsset.Roles;
        upsertAsset.PreservedUri ??= templateAsset.PreservedUri;
        upsertAsset.Reference1 ??= templateAsset.Reference1;
        upsertAsset.Reference2 ??= templateAsset.Reference2;
        upsertAsset.Reference3 ??= templateAsset.Reference3;
        upsertAsset.Error ??= templateAsset.Error;
        upsertAsset.ImageOptimisationPolicy ??= templateAsset.ImageOptimisationPolicy;
        upsertAsset.ThumbnailPolicy ??= templateAsset.ThumbnailPolicy;
        upsertAsset.InitialOrigin ??= templateAsset.InitialOrigin;
        upsertAsset.MediaType ??= templateAsset.MediaType;
        
        // These were previously non-nullable fields
        upsertAsset.Created ??= templateAsset.Created;
        upsertAsset.NumberReference1 ??= templateAsset.NumberReference1;
        upsertAsset.NumberReference2 ??= templateAsset.NumberReference2;
        upsertAsset.NumberReference3 ??= templateAsset.NumberReference3;
        upsertAsset.MaxUnauthorised ??= templateAsset.MaxUnauthorised;
        upsertAsset.Width ??= templateAsset.Width;
        upsertAsset.Height ??= templateAsset.Height;
        upsertAsset.Duration ??= templateAsset.Duration;
        upsertAsset.Batch ??= templateAsset.Batch;
        upsertAsset.Finished ??= templateAsset.Finished;
        upsertAsset.Ingesting ??= templateAsset.Ingesting;
        upsertAsset.Family ??= templateAsset.Family;
    }

    static AssetPreparer()
    {
        // While our Asset object allows nulls, the DB Image row does not (apart from Finished).
        // So we need a set of default values to fill in any nulls.
        DefaultAsset = new Asset
        {
            Id = string.Empty,
            Customer = 0,
            Space = 0,
            Created = DateTime.MinValue.ToUniversalTime(),
            Origin = string.Empty,
            Tags = string.Empty,
            Roles = string.Empty,
            PreservedUri = string.Empty,
            Reference1 = string.Empty,
            Reference2 = string.Empty,
            Reference3 = string.Empty,
            NumberReference1 = 0,
            NumberReference2 = 0,
            NumberReference3 = 0,
            MaxUnauthorised = -1,
            Width = 0,
            Height = 0,
            Duration = 0,
            Error = string.Empty,
            Batch = 0,
            Finished = null,
            Ingesting = false,
            ImageOptimisationPolicy = string.Empty,
            ThumbnailPolicy = string.Empty,
            InitialOrigin = string.Empty,
            Family = AssetFamily.Image,
            MediaType = "unknown"
        };
    }
}