using System.Collections.Generic;
using System.Data;
using API.Features.Image;
using API.Features.Image.Ingest;
using API.Infrastructure;
using API.Infrastructure.Messaging.General;
using API.Infrastructure.Requests;
using DLCS.AWS.SNS.Messaging;
using DLCS.Core;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.Processing;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Queues.Requests;

/// <summary>
/// Create a batch of images from the provided list of assets.
/// </summary>
public class CreateBatchOfImages : IRequest<ModifyEntityResult<Batch>>
{
    public int CustomerId { get; }
    public IReadOnlyList<AssetBeforeProcessing> AssetsBeforeProcessing { get; }
    public bool IsPriority { get; }

    public CreateBatchOfImages(int customerId, IReadOnlyList<AssetBeforeProcessing> assetsBeforeProcessing, string queue = QueueNames.Default)
    {
        CustomerId = customerId;
        AssetsBeforeProcessing = assetsBeforeProcessing;
        IsPriority = queue == QueueNames.Priority;
    }
}

public class CreateBatchOfImagesHandler(
    DlcsContext dlcsContext,
    IBatchRepository batchRepository,
    AssetProcessor assetProcessor,
    IIngestNotificationSender ingestNotificationSender,
    IDeliverableNotificationSender deliverableNotificationSender,
    IBatchCompletedNotificationSender  batchCompletedNotificationSender,
    ILogger<CreateBatchOfImagesHandler> logger)
    : IRequestHandler<CreateBatchOfImages, ModifyEntityResult<Batch>>
{
    public async Task<ModifyEntityResult<Batch>> Handle(CreateBatchOfImages request,
        CancellationToken cancellationToken)
    {
        var priorityValidation = ValidatePriorityQueueRequest(request);
        if (priorityValidation != null) return priorityValidation;

        var spaceValidation = await ValidateAllSpaces(request, cancellationToken);
        if (spaceValidation != null) return spaceValidation;

        var space0Validation = ValidateSpace0DeliveryChannels(request);
        if (space0Validation != null) return space0Validation;

        bool updateFailed = false;
        var failureMessage = string.Empty;
        WriteResult? failureType = null;

        await using var transaction = 
            await dlcsContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        
        var batch = await batchRepository.CreateBatch(request.CustomerId, request.AssetsBeforeProcessing.Select(a => a.Asset).ToList(), cancellationToken);

        var engineNotificationList = new List<Asset>(request.AssetsBeforeProcessing.Count);
        var assetModifiedNotificationList = new List<NotificationRecord<Asset>>();
        
        try
        {
            using var logScope = logger.BeginScope("Processing batch {BatchId}", batch.Id);

            foreach (var assetBeforeProcessing in request.AssetsBeforeProcessing)
            {
                var assetId = assetBeforeProcessing.Asset.Id;
                logger.LogDebug("Processing asset {AssetId}", assetId);
                var processAssetResult =
                    await assetProcessor.Process(assetBeforeProcessing, false, true, true,
                        cancellationToken: cancellationToken);
                if (!processAssetResult.IsSuccess)
                {
                    logger.LogDebug("Processing asset {AssetId} failed, aborting batch. Error: '{Error}'", assetId,
                        processAssetResult.Result.Error);
                    updateFailed = true;
                    failureType = processAssetResult.Result.WriteResult;
                    failureMessage = processAssetResult.Result.Error;
                    break;
                }

                var savedAsset = processAssetResult.Result.Entity!;
                assetModifiedNotificationList.Add(GetAssetModificationRecord(processAssetResult, savedAsset));
                
                if (processAssetResult.RequiresEngineNotification)
                {
                    engineNotificationList.Add(savedAsset);
                    batch.AddBatchAsset(assetId);
                }
                else
                {
                    logger.LogDebug(
                        "Asset {AssetId} of Batch {BatchId} does not require engine notification. Marking as complete",
                        assetId, batch.Id);
                    batch.AddBatchAsset(assetId, BatchStatus.Completed);
                    batch.Completed += 1;
                }
            }
            
            // complete batch if nothing requires notification in the engine
            if (batch.Completed == batch.Count)
            {
                batch.Finished = DateTime.UtcNow;
            }

            await dlcsContext.SaveChangesAsync(cancellationToken);

            if (!updateFailed)
            {
                await transaction.CommitAsync(cancellationToken);
                await deliverableNotificationSender.SendDeliverableModifiedMessage(assetModifiedNotificationList, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception processing batch of {BatchCount}, rolling back", request.AssetsBeforeProcessing.Count);
            updateFailed = true;
            failureMessage = ex.Message;
        }

        if (updateFailed)
        {
            // If the token is already cancelled don't use it - we want these to succeed regardless. Will be rolled
            // back when disposed
            if (!cancellationToken.IsCancellationRequested)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return ModifyEntityResult<Batch>.Failure(failureMessage, failureType ?? WriteResult.Error);
        }

        // Raise notifications
        logger.LogDebug("Batch {BatchId} created - sending engine notifications", batch.Id);
        await ingestNotificationSender.SendIngestAssetsRequest(engineNotificationList, request.IsPriority,
            cancellationToken);

        if (batch.Finished.HasValue)
        {
            logger.LogDebug("Batch {BatchId} completed - sending batch completed notification", batch.Id);
            await batchCompletedNotificationSender.SendBatchCompletedMessage(batch, cancellationToken);
        }

        return ModifyEntityResult<Batch>.Success(batch, WriteResult.Created);
    }

    private static ModifyEntityResult<Batch>? ValidateSpace0DeliveryChannels(CreateBatchOfImages request)
    {
        var hasInvalidSpace0Asset = request.AssetsBeforeProcessing.Any(a =>
            !AssetDeliveryChannels.IsValidForSpaceZero(
                a.Asset.Space,
                a.DeliveryChannelsBeforeProcessing?.Select(dc => dc.Channel)));

        if (!hasInvalidSpace0Asset) return null;

        return ModifyEntityResult<Batch>.Failure(
            "Assets in space 0 can only use the 'none' delivery channel",
            WriteResult.FailedValidation);
    }

    private ModifyEntityResult<Batch>? ValidatePriorityQueueRequest(CreateBatchOfImages request)
    {
        if (!request.IsPriority) return null;

        if (request.AssetsBeforeProcessing.Any(a =>
                a.Asset.Family != AssetFamily.Image && !a.Asset.HasDeliveryChannel(AssetDeliveryChannels.Image) &&
                !MIMEHelper.IsImage(a.Asset.MediaType)))
        {
            return ModifyEntityResult<Batch>.Failure("Priority queue only supports image assets",
                WriteResult.FailedValidation);
        }

        return null;
    }

    private async Task<ModifyEntityResult<Batch>?> ValidateAllSpaces(CreateBatchOfImages request,
        CancellationToken cancellationToken)
    {
        var (exists, missing) = await DoAllSpacesExist(request.CustomerId, request.AssetsBeforeProcessing.Select(a => a.Asset), cancellationToken);
        if (exists) return null;

        var spaceList = string.Join(", ", missing);
        return ModifyEntityResult<Batch>.Failure($"The following space(s) could not be found: {spaceList}",
            WriteResult.FailedValidation);
    }

    private async Task<(bool Exists, IEnumerable<int> NonExistant)> DoAllSpacesExist(int customer,
        IEnumerable<Asset> assets, CancellationToken cancellationToken)
    {
        var uniqueSpaceIds = assets.Select(a => a.Space).Distinct().ToList();
        var matchingIds = await dlcsContext.Spaces
            .Where(s => s.Customer == customer && uniqueSpaceIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (matchingIds.Count == uniqueSpaceIds.Count)
        {
            return (true, Enumerable.Empty<int>());
        }

        var missing = uniqueSpaceIds.Except(matchingIds);
        return (false, missing);
    }
    
    private static NotificationRecord<Asset> GetAssetModificationRecord(ProcessAssetResult processAssetResult,
        Asset savedAsset)
    {
        var existingAsset = processAssetResult.ExistingAsset;
        var assetModificationRecord = existingAsset == null
            ? NotificationRecord<Asset>.Create(savedAsset)
            : NotificationRecord<Asset>.Update(existingAsset, savedAsset, processAssetResult.RequiresEngineNotification);
        return assetModificationRecord;
    }
}
