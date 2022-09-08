using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using API.Exceptions;
using API.Features.Assets;
using API.Infrastructure.Models;
using API.Infrastructure.Requests;
using DLCS.Core;
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
/// Handle create or update requests for assets.
/// This may trigger a sync or async ingest request depending on fields updated and HTTP method used. 
/// </summary>
/// <remarks>Handles PUTs and PATCHes with a single command to ensure same validation happens.</remarks>
public class CreateOrUpdateImage : IRequest<ModifyEntityResult<Asset>>
{
    /// <summary>
    /// The Asset to be updated or inserted; may contain null fields indicating no change
    /// </summary>
    public Asset? Asset { get; }

    /// <summary>
    /// Get a value indicating whether the asset must already exist in db (ie this only supports Update)
    /// </summary>
    public bool AssetMustExist { get; }
    
    /// <summary>
    /// Get a value indicating whether the asset will always be reingested, regardless of which fields are changed
    /// </summary>
    public bool AlwaysReingest { get; }

    public CreateOrUpdateImage(Asset asset, string httpMethod)
    {
        Asset = asset;
        AssetMustExist = httpMethod == "PATCH";
        
        // treat a PUT as a re-process instruction regardless of which values are changed
        AlwaysReingest = httpMethod == "PUT";
    }
}

public class CreateOrUpdateImageHandler : IRequestHandler<CreateOrUpdateImage, ModifyEntityResult<Asset>>
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
    
    public async Task<ModifyEntityResult<Asset>> Handle(CreateOrUpdateImage request, CancellationToken cancellationToken)
    {
        var asset = request.Asset;
        if (asset == null)
        {
            return ModifyEntityResult<Asset>.Failure("Invalid Request", WriteResult.FailedValidation);
        }

        if (!await DoesTargetSpaceExist(asset, cancellationToken))
        {
            return ModifyEntityResult<Asset>.Failure(
                $"Target space for asset does not exist: {asset.Customer}/{asset.Space}",
                WriteResult.FailedValidation);
        }
        
        // Check if Asset already exists
        Asset? existingAsset;
        var changeType = ChangeType.Update;
        try
        {
            existingAsset = await assetRepository.GetAsset(asset.Id, noCache: true);
            if (existingAsset == null)
            {
                if (request.AssetMustExist)
                {
                    return ModifyEntityResult<Asset>.Failure(
                        "Attempted to update an Asset that could not be found",
                        WriteResult.NotFound
                    );
                }

                changeType = ChangeType.Create;
                var counts = await storageRepository.GetImageCounts(asset.Customer, cancellationToken);
                if (counts.CurrentNumberOfStoredImages >= counts.MaximumNumberOfStoredImages)
                {
                    return ModifyEntityResult<Asset>.Failure(
                        $"This operation will fall outside of your storage policy for number of images: maximum is {counts.MaximumNumberOfStoredImages}",
                        WriteResult.StorageLimitExceeded
                    );
                }
            }
        }
        catch (Exception e)
        {
            return ModifyEntityResult<Asset>.Failure(e.Message, WriteResult.Error);
        }

        var assetPreparationResult =
            AssetPreparer.PrepareAssetForUpsert(existingAsset, asset, allowNonApiUpdates: false);
        
        if (!assetPreparationResult.Success)
        {
            return ModifyEntityResult<Asset>.Failure(assetPreparationResult.ErrorMessage, WriteResult.FailedValidation);
        }

        var updatedAsset = assetPreparationResult.UpdatedAsset!;
        var requiresEngineNotification = RequiresEngineNotification(updatedAsset, request, assetPreparationResult);

        // Deliverator only does this for new assets, but it should verify PATCH assets too.
        if (existingAsset == null)
        {
            var imagePolicyChanged = await SelectImageOptimisationPolicy(updatedAsset);
            if (imagePolicyChanged)
            {
                // NB the AssetPreparer has already inspected image policy, but this will pick up
                // a change from default.
                requiresEngineNotification = true;
            }
            var thumbnailPolicyChanged = await SelectThumbnailPolicy(updatedAsset);
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
            updatedAsset.SetFieldsForIngestion();
            
            if (updatedAsset.Family == AssetFamily.Timebased)
            {
                // Timebased asset - create a Batch record in DB and populate Batch property in Asset
                await batchRepository.CreateBatch(updatedAsset.Customer, updatedAsset.AsList(), cancellationToken);
            }
        }

        var assetAfterSave = await assetRepository.Save(updatedAsset, cancellationToken);

        // Restore fields that are not persisted but are required
        if (updatedAsset.InitialOrigin.HasText())
        {
            assetAfterSave.InitialOrigin = asset.InitialOrigin;
        }

        await assetNotificationSender.SendAssetModifiedNotification(changeType, existingAsset, assetAfterSave);

        if (requiresEngineNotification)
        {
            return await IngestAndGenerateResult(assetAfterSave, existingAsset != null, cancellationToken);
        }

        return ModifyEntityResult<Asset>.Success(assetAfterSave,
            existingAsset == null ? WriteResult.Created : WriteResult.Updated);
    }

    private static bool RequiresEngineNotification(Asset asset, CreateOrUpdateImage request, 
        AssetPreparationResult assetPreparationResult)
    {
        // A 'File' never results in the engine being called
        if (asset.Family == AssetFamily.File) return false;
        
        return assetPreparationResult.RequiresReingest || request.AlwaysReingest;
    }

    private async Task<bool> DoesTargetSpaceExist(Asset asset, CancellationToken cancellationToken)
    {
        var targetSpace = await spaceRepository.GetSpace(asset.Customer, asset.Space, false, cancellationToken);
        return targetSpace != null;
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
    
    private async Task<ModifyEntityResult<Asset>> IngestAndGenerateResult(Asset asset, bool isUpdate,
        CancellationToken cancellationToken)
    {
        async Task<ModifyEntityResult<Asset>> GenerateFinalResult(bool success, string errorMessage,
            HttpStatusCode errorCode)
        {
            if (success)
            {
                // obtain it again after Engine has processed it
                var assetAfterEngine = await assetRepository.GetAsset(asset.Id, noCache: true);
                return ModifyEntityResult<Asset>.Success(assetAfterEngine,
                    isUpdate ? WriteResult.Updated : WriteResult.Created);
            }

            throw new APIException(errorMessage) { StatusCode = (int)errorCode };
        }

        switch (asset.Family)
        {
            case AssetFamily.Image:
            {
                // await call to engine, which processes synchronously (not a queue)
                var statusCode =
                    await assetNotificationSender.SendImmediateIngestAssetRequest(asset, false,
                        cancellationToken);
                var success = statusCode is HttpStatusCode.Created or HttpStatusCode.OK;

                // NOTE(DG) - do we want to pass the downstream status regardless?
                return await GenerateFinalResult(success, "Engine was not able to process this asset", statusCode);
            }
            case AssetFamily.Timebased:
            {
                // Queue record for ingestion
                var success =
                    await assetNotificationSender.SendIngestAssetRequest(asset, cancellationToken);

                return await GenerateFinalResult(success, "Unable to queue for processing",
                    HttpStatusCode.InternalServerError);
            }
            default:
                throw new ArgumentOutOfRangeException($"No engine logic for asset family {asset.Family}");
        }
    }
}
