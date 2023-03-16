using System;
using System.Linq;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Policies;

namespace DLCS.Model.Assets;

/// <summary>
/// Conveys whether an attempt to prepare an asset for upsert encountered an invalid state.
/// </summary>
public record AssetPreparationResult
{
    /// <summary>
    /// The asset is OK to go to the database
    /// </summary>
    public bool Success { get; private init; }
    
    /// <summary>
    /// The asset cannot go to the DB for this reason.
    /// </summary>
    public string? ErrorMessage { get; private init; }
    
    /// <summary>
    /// As aspect of the Asset has changed that means it needs to be re-processed by Engine.
    /// </summary>
    public bool RequiresReingest { get; private init; }
    
    /// <summary>
    /// The final asset that contains reconciled values.
    /// This will either be an existing asset that has been updated; or  
    /// </summary>
    public Asset? UpdatedAsset { get; private init; }

    /// <summary>
    /// Create a new <see cref="AssetPreparationResult"/> that represents a failure
    /// </summary>
    public static AssetPreparationResult Failure(string errorMessage)
        => new() { ErrorMessage = errorMessage };

    /// <summary>
    /// Create a new <see cref="AssetPreparationResult"/> that represents a successful operation
    /// </summary>
    public static AssetPreparationResult Succeed(Asset updatedAsset, bool requiresReingest)
        => new() { Success = true, RequiresReingest = requiresReingest, UpdatedAsset = updatedAsset };
}
    
/// <summary>
/// Prepares an asset for insert or update operations.
/// </summary>
public static class AssetPreparer
{
    private static readonly Asset DefaultAsset;

    /// <summary>
    /// Prepares an asset for insert or update operations by
    /// Validates that things aren't being modified that shouldn't be,
    /// and that no null fields remain on the asset being upserted.
    /// </summary>
    /// <remarks>
    /// This is essentially the logic in 
    /// https://github.com/digirati-co-uk/deliverator/blob/master/DLCS.Application/Behaviour/API/ValidateImageUpsertBehaviour.cs
    /// </remarks>
    /// <param name="existingAsset">If this is an update, the current version of the asset</param>
    /// <param name="updateAsset">
    /// The new or updated asset - this is the submitted list of changes
    /// </param>
    /// <param name="allowNonApiUpdates">
    /// Permit setting of fields that would not be allowed on API calls. Use with caution - all values submitted will
    /// be saved as this effectively drops validation.
    /// </param>
    /// <param name="isBatchUpdate">True if this is part of batch creation - allows Batch value to be set.</param>
    /// <returns>A validation result</returns>
    public static AssetPreparationResult PrepareAssetForUpsert(
        Asset? existingAsset,
        Asset updateAsset,
        bool allowNonApiUpdates,
        bool isBatchUpdate)
    {
        bool requiresReingest = existingAsset == null;
        
        // Set creation date - if this is a create
        if ((updateAsset.Created == null || updateAsset.Created == DateTime.MinValue) && existingAsset == null)
        {
            updateAsset.Created = DateTime.UtcNow;
        }

        // Validate there are no issues
        var prepareAssetForUpsert = ValidateRequests(existingAsset, updateAsset, allowNonApiUpdates, isBatchUpdate);
        if (prepareAssetForUpsert != null) return prepareAssetForUpsert;

        if (existingAsset != null)
        {
            if (updateAsset.Origin.HasText() && updateAsset.Origin != existingAsset.Origin)
            {
                requiresReingest = true;
            }

            if (updateAsset.DeliveryChannel != null &&
                !updateAsset.DeliveryChannel.SequenceEqual(existingAsset.DeliveryChannel))
            {
                // Changing DeliveryChannel can alter how the image should be processed
                requiresReingest = true;
            }
            
            if (updateAsset.ThumbnailPolicy.HasText() && updateAsset.ThumbnailPolicy != existingAsset.ThumbnailPolicy)
            {
                // requiresReingest = true; NO, because we'll re-create thumbs on demand - "backfill"
                // However, we can treat a PUT as always triggering reingest, whereas a PATCH does not,
                // even if they are otherwise equivalent - see CreateOrUpdateImage
            }

            if (updateAsset.ImageOptimisationPolicy.HasText() &&
                updateAsset.ImageOptimisationPolicy != existingAsset.ImageOptimisationPolicy)
            {
                requiresReingest = true; // YES, because we've changed the way this image should be processed
            }
        }

        var workingAsset = existingAsset ?? updateAsset;

        if (existingAsset == null)
        {
            // Creation of new asset - the DB record is what's been submitted with any NULLs replaced by default
            workingAsset.DefaultNullProperties(DefaultAsset);
        }
        else
        {
            // Update existing asset - the DB record is what was in DB with any submitted changes applied
            workingAsset.ApplyChanges(updateAsset);
        }
        
        if (requiresReingest && workingAsset.Origin.IsNullOrEmpty())
        {
            return AssetPreparationResult.Failure("Asset Origin must be supplied.");
        }

        // 'File' family assets are never ingested so default back to false regardless of value
        if (workingAsset.Family == AssetFamily.File)
        {
            requiresReingest = false;
        }

        return AssetPreparationResult.Succeed(workingAsset, requiresReingest);
    }

    private static AssetPreparationResult? ValidateRequests(Asset? existingAsset, Asset updateAsset,
        bool allowNonApiUpdates, bool isBatchUpdate)
    {
        if (existingAsset is { NotForDelivery: true })
        {
            // We can relax this later but for now, you cannot use the API
            // to modify an asset marked NotForDelivery.
            return AssetPreparationResult.Failure("Cannot use API to modify a NotForDelivery asset.");
            // However, this DOES allow the *creation* of a NotForDelivery asset.
        }

        if (!updateAsset.DeliveryChannel.IsNullOrEmpty())
        {
            foreach (var dc in updateAsset.DeliveryChannel)
            {
                if (!AssetDeliveryChannels.All.Contains(dc))
                {
                    return AssetPreparationResult.Failure(
                        $"'{dc}' is an invalid deliveryChannel. Valid values are: {AssetDeliveryChannels.AllString}.");
                }
            }
        }
        
        if (allowNonApiUpdates == false)
        {
            // These cannot be created or modified via the API
            if (updateAsset.Finished.HasValue)
            {
                return AssetPreparationResult.Failure("Cannot set Finished timestamp via API.");
            }

            if (updateAsset.Error != null)
            {
                return AssetPreparationResult.Failure("Cannot set Error state via API.");
            }
        }

        if (existingAsset != null)
        {
            if (updateAsset.Customer != existingAsset.Customer)
            {
                return AssetPreparationResult.Failure("Cannot change an Assets customer.");
            }

            if (updateAsset.Space != existingAsset.Space)
            {
                return AssetPreparationResult.Failure("Cannot change an Assets space.");
            }
        }

        // If we have an existing Asset and we are not allowed nonApiUpdates
        if (existingAsset != null && allowNonApiUpdates == false)
        {
            bool isNoOpPolicy = KnownImageOptimisationPolicy.IsNoOpIdentifier(existingAsset.ImageOptimisationPolicy);
            
            if (updateAsset.Width.HasValue && updateAsset.Width != 0 && updateAsset.Width != existingAsset.Width)
            {
                // if it's a policy other than "none" or it is an audio asset then this isn't valid
                if (!isNoOpPolicy || MIMEHelper.IsAudio(existingAsset.MediaType))
                {
                    return AssetPreparationResult.Failure("Width cannot be edited.");
                }
            }

            if (updateAsset.Height.HasValue && updateAsset.Height != 0 && updateAsset.Height != existingAsset.Height)
            {
                // if it's a policy other than "none" or it is an audio asset then this isn't valid
                if (!isNoOpPolicy || MIMEHelper.IsAudio(existingAsset.MediaType))
                {
                    return AssetPreparationResult.Failure("Height cannot be edited.");
                }
            }

            if (updateAsset.Duration.HasValue && updateAsset.Duration != 0 &&
                updateAsset.Duration != existingAsset.Duration)
            {
                // if it's a policy other than "none" or family other than Timebased then isn't valid
                if (!isNoOpPolicy || existingAsset.Family != AssetFamily.Timebased)
                {
                    return AssetPreparationResult.Failure("Duration cannot be edited.");
                }
            }

            if (updateAsset.PreservedUri != null && updateAsset.PreservedUri != existingAsset.PreservedUri)
            {
                return AssetPreparationResult.Failure("PreservedUri cannot be edited.");
            }

            if (updateAsset.Error != null && updateAsset.Error != existingAsset.Error)
            {
                return AssetPreparationResult.Failure("Error cannot be edited.");
            }

            if (!isBatchUpdate && updateAsset.Batch.HasValue && updateAsset.Batch != 0 &&
                updateAsset.Batch != existingAsset.Batch)
            {
                return AssetPreparationResult.Failure("Batch cannot be edited.");
            }

            if (updateAsset.ImageOptimisationPolicy != null &&
                updateAsset.ImageOptimisationPolicy != existingAsset.ImageOptimisationPolicy)
            {
                return AssetPreparationResult.Failure("ImageOptimisationPolicy cannot be edited.");
            }

            if (updateAsset.ThumbnailPolicy != null && updateAsset.ThumbnailPolicy != existingAsset.ThumbnailPolicy)
            {
                return AssetPreparationResult.Failure("ThumbnailPolicy cannot be edited.");
            }

            if (updateAsset.Family != null && updateAsset.Family != existingAsset.Family)
            {
                return AssetPreparationResult.Failure("Family cannot be edited.");
            }

            if (updateAsset.InitialOrigin != null)
            {
                return AssetPreparationResult.Failure("Cannot edit the InitialOrigin of an asset.");
            }
        }

        return null;
    }

    static AssetPreparer()
    {
        // While our Asset object allows nulls, the DB Image row does not (apart from Finished).
        // So we need a set of default values to fill in any nulls.
        DefaultAsset = new Asset
        {
            Id = null,
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
            DeliveryChannel = null,
            MediaType = "unknown"
        };
    }
}