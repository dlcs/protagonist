using System.Collections.Generic;
using System.Data;
using API.Features.Image;
using API.Features.Image.Ingest;
using API.Infrastructure.Messaging;
using API.Infrastructure.Requests;
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

public class CreateBatchOfImagesHandler : IRequestHandler<CreateBatchOfImages, ModifyEntityResult<Batch>>
{
    private readonly DlcsContext dlcsContext;
    private readonly IBatchRepository batchRepository;
    private readonly AssetProcessor assetProcessor;
    private readonly IIngestNotificationSender ingestNotificationSender;
    private readonly IAssetNotificationSender assetNotificationSender;
    private readonly ILogger<CreateBatchOfImagesHandler> logger;

    public CreateBatchOfImagesHandler(
        DlcsContext dlcsContext,
        IBatchRepository batchRepository,
        AssetProcessor assetProcessor,
        IIngestNotificationSender ingestNotificationSender,
        IAssetNotificationSender assetNotificationSender,
        ILogger<CreateBatchOfImagesHandler> logger)
    {
        this.dlcsContext = dlcsContext;
        this.batchRepository = batchRepository;
        this.assetProcessor = assetProcessor;
        this.ingestNotificationSender = ingestNotificationSender;
        this.assetNotificationSender = assetNotificationSender;
        this.logger = logger;
    }

    public async Task<ModifyEntityResult<Batch>> Handle(CreateBatchOfImages request,
        CancellationToken cancellationToken)
    {
        // TODO - we may need to support non-Image assets here 
        if (request.IsPriority)
        {
            if (request.AssetsBeforeProcessing.Any(a =>
                    a.Asset.Family != AssetFamily.Image && !a.Asset.HasDeliveryChannel(AssetDeliveryChannels.Image) &&
                    !MIMEHelper.IsImage(a.Asset.MediaType)))
            {
                return ModifyEntityResult<Batch>.Failure("Priority queue only supports image assets",
                    WriteResult.FailedValidation);
            }
        }
        
        var (exists, missing) = await DoAllSpacesExist(request.CustomerId, request.AssetsBeforeProcessing.Select(a => a.Asset), cancellationToken);
        if (!exists)
        {
            var spaceList = string.Join(", ", missing);
            return ModifyEntityResult<Batch>.Failure($"The following space(s) could not be found: {spaceList}",
                WriteResult.FailedValidation);
        }

        bool updateFailed = false;
        var failureMessage = string.Empty;

        await using var transaction = 
            await dlcsContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        
        var batch = await batchRepository.CreateBatch(request.CustomerId, request.AssetsBeforeProcessing.Select(a => a.Asset).ToList(), cancellationToken);

        var engineNotificationList = new List<Asset>(request.AssetsBeforeProcessing.Count);
        var assetModifiedNotificationList = new List<AssetModificationRecord>();

        try
        {
            using var logScope = logger.BeginScope("Processing batch {BatchId}", batch.Id);

            foreach (var assetBeforeProcessing in request.AssetsBeforeProcessing)
            {
                logger.LogDebug("Processing asset {AssetId}", assetBeforeProcessing.Asset.Id);
                var processAssetResult =
                    await assetProcessor.Process(assetBeforeProcessing, false, true, true,
                        cancellationToken: cancellationToken);
                if (!processAssetResult.IsSuccess)
                {
                    logger.LogDebug("Processing asset {AssetId} failed, aborting batch. Error: '{Error}'", assetBeforeProcessing.Asset.Id,
                        processAssetResult.Result.Error);
                    updateFailed = true;
                    failureMessage = processAssetResult.Result.Error;
                    break;
                }

                var savedAsset = processAssetResult.Result.Entity!;
                
                var existingAsset = processAssetResult.ExistingAsset;
                var assetModificationRecord = existingAsset == null
                    ? AssetModificationRecord.Create(savedAsset)
                    : AssetModificationRecord.Update(existingAsset, savedAsset, processAssetResult.RequiresEngineNotification);
                assetModifiedNotificationList.Add(assetModificationRecord);
                
                if (processAssetResult.RequiresEngineNotification)
                {
                    engineNotificationList.Add(savedAsset);
                }
                else
                {
                    logger.LogDebug(
                        "Asset {AssetId} of Batch {BatchId} does not require engine notification. Marking as complete",
                        assetBeforeProcessing.Asset.Id, batch.Id);
                    batch.Completed += 1;
                }
            }
            
            await assetNotificationSender.SendAssetModifiedMessage(assetModifiedNotificationList, cancellationToken);

            if (batch.Completed > 0)
            {
                await dlcsContext.SaveChangesAsync(cancellationToken);
            }

            if (!updateFailed)
            {
                await transaction.CommitAsync(cancellationToken);
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

            return ModifyEntityResult<Batch>.Failure(failureMessage, WriteResult.Error);
        }
        else
        {
            // Raise notifications
            logger.LogDebug("Batch {BatchId} created - sending engine notifications", batch.Id);
            await ingestNotificationSender.SendIngestAssetsRequest(engineNotificationList, request.IsPriority,
                cancellationToken);
        }
        
        return ModifyEntityResult<Batch>.Success(batch, WriteResult.Created);
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
}