using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Features.Image.Ingest;
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
    public IReadOnlyList<Asset> Assets { get; }
    public bool IsPriority { get; }

    public CreateBatchOfImages(int customerId, IReadOnlyList<Asset> assets, string queue = QueueNames.Default)
    {
        CustomerId = customerId;
        Assets = assets;
        IsPriority = queue == QueueNames.Priority;
    }
}

public class CreateBatchOfImagesHandler : IRequestHandler<CreateBatchOfImages, ModifyEntityResult<Batch>>
{
    private readonly DlcsContext dlcsContext;
    private readonly IBatchRepository batchRepository;
    private readonly AssetProcessor assetProcessor;
    private readonly IAssetNotificationSender assetNotificationSender;
    private readonly ILogger<CreateBatchOfImagesHandler> logger;

    public CreateBatchOfImagesHandler(
        DlcsContext dlcsContext,
        IBatchRepository batchRepository,
        AssetProcessor assetProcessor,
        IAssetNotificationSender assetNotificationSender,
        ILogger<CreateBatchOfImagesHandler> logger)
    {
        this.dlcsContext = dlcsContext;
        this.batchRepository = batchRepository;
        this.assetProcessor = assetProcessor;
        this.assetNotificationSender = assetNotificationSender;
        this.logger = logger;
    }

    public async Task<ModifyEntityResult<Batch>> Handle(CreateBatchOfImages request,
        CancellationToken cancellationToken)
    {
        var (exists, missing) = await DoAllSpacesExist(request.CustomerId, request.Assets, cancellationToken);
        if (!exists)
        {
            var spaceList = string.Join(", ", missing);
            return ModifyEntityResult<Batch>.Failure($"The following space(s) could not be found: {spaceList}",
                WriteResult.FailedValidation);
        }

        bool updateFailed = false;
        var failureMessage = string.Empty;
        var batch = await batchRepository.CreateBatch(request.CustomerId, request.Assets, cancellationToken);
        
        await using var transaction = 
            await dlcsContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var assetNotificationList = new List<Asset>(request.Assets.Count);
        try
        {
            using var logScope = logger.BeginScope("Processing batch {BatchId}", batch.Id);

            foreach (var asset in request.Assets)
            {
                logger.LogDebug("Processing asset {AssetId}", asset.Id);
                var processAssetResult =
                    await assetProcessor.Process(asset, false, true, true, cancellationToken: cancellationToken);
                if (!processAssetResult.IsSuccess)
                {
                    logger.LogDebug("Processing asset {AssetId} failed, aborting batch", asset.Id);
                    updateFailed = true;
                    failureMessage = processAssetResult.Result.Error;
                    break;
                }

                var savedAsset = processAssetResult.Result.Entity!;
                
                if (processAssetResult.RequiresEngineNotification)
                {
                    assetNotificationList.Add(savedAsset);
                }
                else
                {
                    logger.LogDebug("Asset {AssetId} is file, marking as complete", asset.Id);
                    batch.Completed += 1;
                }
            }

            if (batch.Completed > 0)
            {
                dlcsContext.Batches.Attach(batch);
                dlcsContext.Entry(batch).State = EntityState.Modified;
            }

            if (!updateFailed)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception processing batch of {BatchCount}, rolling back", request.Assets.Count);
            updateFailed = true;
            failureMessage = ex.Message;
        }

        if (updateFailed)
        {
            await transaction.RollbackAsync(cancellationToken);
            
            dlcsContext.Batches.Remove(batch);
            await dlcsContext.SaveChangesAsync(cancellationToken);
            
            return ModifyEntityResult<Batch>.Failure(failureMessage, WriteResult.Error);
        }
        else
        {
            // Raise notifications
            await assetNotificationSender.SendIngestAssetsRequest(assetNotificationList, request.IsPriority,
                cancellationToken);
        }
        
        return ModifyEntityResult<Batch>.Success(batch);
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