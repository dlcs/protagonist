using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using API.Features.Assets;
using DLCS.Core.Collections;
using DLCS.Core.Settings;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
using DLCS.Model.Spaces;
using DLCS.Model.Storage;
using MediatR;
using Microsoft.Extensions.Options;

namespace API.Features.Image.Requests;

/// <summary>
/// Handle PUTs and PATCHes with a single command to ensure same validation happens.
/// </summary>
public class CreateOrUpdateImage : IRequest<CreateOrUpdateImageResult>
{
    /// <summary>
    /// The Asset to be updated or inserted; may contain null fields indicating no change
    /// </summary>
    public Asset? Asset { get; set; }

    /// <summary>
    /// PUT or PATCH
    /// (While this leaks HTTP into the Mediatr it's clearer than using Create or Update) 
    /// </summary>
    public string? Method { get; set; }
}

/// <summary>
/// The outcome of an update or insert operation
/// </summary>
public class CreateOrUpdateImageResult
{
    /// <summary>
    /// The asset, which may have been processed by Engine during this operation
    /// </summary>
    public Asset? Asset { get; set; }
    
    /// <summary>
    /// The appropriate status to return to the API caller.
    /// </summary>
    public HttpStatusCode? StatusCode { get; set; }
    
    /// <summary>
    /// An error message, if anything went wrong.
    /// </summary>
    public string? Message { get; set; }
}

public class CreateOrUpdateImageHandler : IRequestHandler<CreateOrUpdateImage, CreateOrUpdateImageResult>
{
    private readonly ISpaceRepository spaceRepository;
    private readonly IApiAssetRepository assetRepository;
    private readonly IStorageRepository storageRepository;
    private readonly IPolicyRepository policyRepository;
    private readonly IBatchRepository batchRepository;
    private readonly IAssetNotificationSender assetNotificationSender;
    private readonly DlcsSettings settings;

    public CreateOrUpdateImageHandler(
        ISpaceRepository spaceRepository,
        IApiAssetRepository assetRepository,
        IStorageRepository storageRepository,
        IPolicyRepository policyRepository,
        IBatchRepository batchRepository,
        IAssetNotificationSender assetNotificationSender,
        IOptions<DlcsSettings> dlcsSettings)
    {
        this.spaceRepository = spaceRepository;
        this.assetRepository = assetRepository;
        this.storageRepository = storageRepository;
        this.policyRepository = policyRepository;
        this.batchRepository = batchRepository;
        this.assetNotificationSender = assetNotificationSender;
        this.settings = dlcsSettings.Value;
    }
    
    public async Task<CreateOrUpdateImageResult> Handle(CreateOrUpdateImage request, CancellationToken cancellationToken)
    {
        var asset = request.Asset;
        var changeType = ChangeType.Update;
        if (asset == null || request.Method == null)
        {
            return new CreateOrUpdateImageResult
            {
                StatusCode = HttpStatusCode.BadRequest,
                Message = "Invalid Request"
            };
        }
        
        if (request.Method is not ("PUT" or "PATCH"))
        {
            return new CreateOrUpdateImageResult
            {
                StatusCode = HttpStatusCode.MethodNotAllowed,
                Message = "Method must be PUT or PATCH"
            };
        }
        
        if (!await DoesTargetSpaceExist(asset, cancellationToken))
        {
            return new CreateOrUpdateImageResult
            {
                StatusCode = HttpStatusCode.BadRequest,
                Message = $"Target space for asset does not exist: {asset.Customer}/{asset.Space}"
            };
        }
        
        // Check if image already exists
        Asset? existingAsset;
        try
        {
            existingAsset = await assetRepository.GetAsset(asset.Id, noCache: true);
            if (existingAsset == null)
            {
                changeType = ChangeType.Create;
                if (request.Method == "PATCH")
                {
                    return new CreateOrUpdateImageResult
                    {
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "Attempted to PATCH an Asset that could not be found."
                    };
                }

                var counts = await storageRepository.GetImageCounts(asset.Customer, cancellationToken);
                if (counts.CurrentNumberOfStoredImages >= counts.MaximumNumberOfStoredImages)
                {
                    return new CreateOrUpdateImageResult
                    {
                        StatusCode = HttpStatusCode.InsufficientStorage,
                        Message = $"This operation will fall outside of your storage policy for number of images: maximum is {counts.MaximumNumberOfStoredImages}"
                    };
                }
            }
        }
        catch (Exception e)
        {
            return new CreateOrUpdateImageResult
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Message = e.Message
            };
        }

        var assetPreparationResult = AssetPreparer.PrepareAssetForUpsert(existingAsset, asset, allowNonApiUpdates: false);
        
        if (!assetPreparationResult.Success)
        {
            // ValidateImageUpsertBehaviour (though has been modified quite a bit)
            return new CreateOrUpdateImageResult
            {
                StatusCode = HttpStatusCode.BadRequest,
                Message = assetPreparationResult.ErrorMessage
            };
        }

        // we treat a PUT as a re-process instruction regardless
        var requiresEngineNotification = assetPreparationResult.RequiresReingest || request.Method == "PUT";

        // Deliverator only does this for new assets, but it should verify PATCH assets too.
        if (existingAsset == null)
        {
            var imagePolicyChanged = await SelectImageOptimisationPolicy(asset);
            if (imagePolicyChanged)
            {
                // NB the AssetPreparer has already inspected image policy, but this will pick up
                // a change from default.
                requiresEngineNotification = true;
            }
            var thumbnailPolicyChanged = await SelectThumbnailPolicy(asset);
            if (thumbnailPolicyChanged)
            {
                // We won't alter the value of requiresEngineNotification
                // TODO thumbs will be backfilled.
                // This could be a config setting.
            }
        }

        // If a re-process is required, clear out fields related to processing
        if (requiresEngineNotification)
        {
            ResetFieldsForIngestion(asset);
            
            if (asset.Family == AssetFamily.Timebased)
            {
                // Timebased asset - create a Batch record in DB and populate Batch property in Asset
                await batchRepository.CreateBatch(asset.Customer, asset.AsList(), cancellationToken);
            }
        }

        await assetRepository.Save(asset, cancellationToken);

        // now obtain the asset again
        var assetAfterSave = await assetRepository.GetAsset(asset.Id, noCache: true);
        if (assetAfterSave == null)
        {
            return new CreateOrUpdateImageResult
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Message = "Error reloading asset after save"
            };
        }
        
        // Restore fields that are not persisted but are required
        if (asset.InitialOrigin.HasText())
        {
            assetAfterSave.InitialOrigin = asset.InitialOrigin;
        }

        async Task<CreateOrUpdateImageResult> GenerateFinalResult(bool success, string errorMessage,
            HttpStatusCode errorCode)
        {
            if (success)
            {
                // obtain it again after Engine has processed it
                var assetAfterEngine = await assetRepository.GetAsset(asset!.Id, noCache: true);
                return new CreateOrUpdateImageResult
                {
                    Asset = assetAfterEngine,
                    StatusCode = existingAsset == null ? HttpStatusCode.Created : HttpStatusCode.OK
                };
            }

            return new CreateOrUpdateImageResult
            {
                Asset = assetAfterSave,
                Message = errorMessage,
                StatusCode = errorCode
            };
        }

        await assetNotificationSender.SendAssetModifiedNotification(changeType, existingAsset, assetAfterSave);

        if (asset.Family == AssetFamily.Image)
        {
            if (requiresEngineNotification)
            {
                // await call to engine load-balancer, which processes synchronously (not a queue)
                var statusCode =
                    await assetNotificationSender.SendImmediateIngestAssetRequest(assetAfterSave, false,
                        cancellationToken);
                var success = statusCode is HttpStatusCode.Created or HttpStatusCode.OK;

                // NOTE(DG) - do we want to pass the downstream status regardless?
                return await GenerateFinalResult(success, "Engine was not able to process this asset", statusCode);
            }

            return new CreateOrUpdateImageResult
            {
                Asset = assetAfterSave,
                StatusCode = existingAsset == null ? HttpStatusCode.Created : HttpStatusCode.OK
            };
        }
        else if (asset.Family == AssetFamily.Timebased)
        {
            if (requiresEngineNotification)
            {
                // Queue record for ingestion
                var success = await assetNotificationSender.SendIngestAssetRequest(assetAfterSave, cancellationToken);

                return await GenerateFinalResult(success, "Unable to queue for processing",
                    HttpStatusCode.InternalServerError);
            }
            
            return new CreateOrUpdateImageResult
            {
                Asset = assetAfterSave,
                StatusCode = existingAsset == null ? HttpStatusCode.Created : HttpStatusCode.OK
            };
        }

        // return 200 or 201 or 500
        // else
        // TODO - this should go once we have F and T handled
        return new CreateOrUpdateImageResult
        {
            StatusCode = HttpStatusCode.NotImplemented,
            Message = "This handler has a limited implementation for now."
        };
    }

    private async Task<bool> DoesTargetSpaceExist(Asset asset, CancellationToken cancellationToken)
    {
        var targetSpace = await spaceRepository.GetSpace(asset.Customer, asset.Space, false, cancellationToken);
        return targetSpace != null;
    }

    private static void ResetFieldsForIngestion(Asset asset)
    {
        asset.Error = string.Empty;
        asset.Ingesting = true;
    }

    private async Task<bool> SelectThumbnailPolicy(Asset asset)
    {
        bool changed = false;
        if (asset.Family == AssetFamily.Image)
        {
            changed = await SetThumbnailPolicy(settings.IngestDefaults.ThumbnailPolicies.Graphics, asset);
        }
        else if (asset.Family == AssetFamily.Timebased && asset.MediaType.HasText() && asset.MediaType.Contains("video/"))
        {
            changed = await SetThumbnailPolicy(settings.IngestDefaults.ThumbnailPolicies.Video, asset);
        }

        return changed;
    }

    private async Task<bool> SelectImageOptimisationPolicy(Asset asset)
    {
        bool changed = false;
        if (asset.Family == AssetFamily.Image)
        {
            changed = await SetImagePolicy(settings.IngestDefaults.ImageOptimisationPolicies.Graphics, asset);
        }
        else if (asset.Family == AssetFamily.Timebased && asset.MediaType.HasText())
        {
            if (asset.MediaType.Contains("video/"))
            {
                changed = await SetImagePolicy(settings.IngestDefaults.ImageOptimisationPolicies.Video, asset);
            }
            else if (asset.MediaType.Contains("audio/"))
            {
                changed = await SetImagePolicy(settings.IngestDefaults.ImageOptimisationPolicies.Audio, asset);
            }
        }

        return changed;
    }

    private async Task<bool> SetImagePolicy(string key, Asset asset)
    {
        string? incomingPolicy = asset.ImageOptimisationPolicy;
        ImageOptimisationPolicy? policy = null;
        if (incomingPolicy.HasText())
        {
            policy = await policyRepository.GetImageOptimisationPolicy(incomingPolicy);
        }

        if (policy == null)
        {
            // The asset doesn't have a valid ImageOptimisationPolicy
            // This is adapted from Deliverator, but there wasn't a way of 
            // taking the policy from the incoming PUT. There now is.
            var imagePolicy = await policyRepository.GetImageOptimisationPolicy(key);
            if (imagePolicy != null)
            {
                asset.ImageOptimisationPolicy = imagePolicy.Id;
            }
        }

        return asset.ImageOptimisationPolicy != incomingPolicy;
    }
    
    private async Task<bool> SetThumbnailPolicy(string key, Asset asset)
    {
        string? incomingPolicy = asset.ThumbnailPolicy;
        ThumbnailPolicy? policy = null;
        if (incomingPolicy.HasText())
        {
            policy = await policyRepository.GetThumbnailPolicy(incomingPolicy);
        }

        if (policy == null)
        {
            var thumbnailPolicy = await policyRepository.GetThumbnailPolicy(key);
            if (thumbnailPolicy != null)
            {
                asset.ThumbnailPolicy = thumbnailPolicy.Id;
            }
        }

        return asset.ThumbnailPolicy != incomingPolicy;
    }
}
