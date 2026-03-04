using DLCS.AWS.SNS.Messaging;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using Microsoft.EntityFrameworkCore;

namespace Engine.Data;

public class EngineAssetRepository(
    DlcsContext dlcsContext,
    IBatchCompletedNotificationSender batchCompletedNotificationSender,
    ILogger<EngineAssetRepository> logger)
    : IEngineAssetRepository, IDapperContextRepository
{
    public DlcsContext DlcsContext { get; } = dlcsContext;

    public async Task<bool> UpdateIngestedDeliverable(IDeliverable deliverable, ImageLocation? imageLocation, ImageStorage? imageStorage,
        bool ingestFinished, CancellationToken cancellationToken = default)
    {
        var hasBatch = deliverable is Asset { BatchAssets.Count: > 0 };

        logger.LogDebug("Updating ingested item {Item}. HasBatch:{HasBatch}, Finished:{Finished}", deliverable.Identifier(),
            hasBatch, ingestFinished);

        var assetId = deliverable.GetAssetId();
        
        try
        {
            UpdateDeliverable(deliverable, ingestFinished);

            if (imageLocation != null)
            {
                if (await DlcsContext.ImageLocations.AnyAsync(l => l.Id == assetId, cancellationToken))
                {
                    DlcsContext.ImageLocations.Attach(imageLocation);
                    DlcsContext.Entry(imageLocation).State = EntityState.Modified;
                }
                else
                {
                    DlcsContext.ImageLocations.Add(imageLocation);
                }
            }

            await UpsertImageStorage(assetId, imageStorage, cancellationToken);
            
            var updatedRows = hasBatch && deliverable is Asset asset
                ? await BatchSave(asset, ingestFinished, cancellationToken)
                : await NonBatchedSave(cancellationToken);

            if (updatedRows && imageStorage != null)
            {
                await IncreaseCustomerStorage(imageStorage, cancellationToken);
            }
            
            return updatedRows || !ingestFinished; // if the ingestion hasn't finished, rows can be not updated - meaning success
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finalising item {AssetId} in DB", deliverable.Identifier());
            return false;
        }
    }

    public ValueTask<Asset?> GetAsset(AssetId assetId, int? batchId, CancellationToken cancellationToken = default)
    {
        var images = DlcsContext.Images
            .Include(i => i.AssetApplicationMetadata)
            .IncludeDeliveryChannelsWithPolicy();

        if (batchId.HasValue)
        {
            images = images.Include(i => i.BatchAssets.Where(ba => ba.BatchId == batchId.Value));
        }
        
        return new ValueTask<Asset?>(images.SingleOrDefaultAsync(i => i.Id == assetId, cancellationToken));
    }

    public ValueTask<Adjunct?> GetAdjunct(string id, AssetId assetId, CancellationToken cancellationToken = default)
        => new(
            DlcsContext.Adjuncts
            .Include(a => a.Asset)
            .SingleOrDefaultAsync(a => a.Id == id && a.AssetId == assetId, cancellationToken)
        );

    public ValueTask<ImageStorage?> GetImageStorage(AssetId assetId, CancellationToken cancellationToken = default)
        => new(DlcsContext.ImageStorages.SingleOrDefaultAsync(i => i.Id == assetId, cancellationToken));

    public async Task<long?> GetImageSize(AssetId assetId, CancellationToken cancellationToken = default)
    {
        var imageSize = await DlcsContext.ImageStorages.AsNoTracking()
            .Where(i => i.Id == assetId)
            .Select(i => i.Size)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        return imageSize;
    }
    
    private async Task UpsertImageStorage(AssetId assetId, ImageStorage? imageStorage,
        CancellationToken cancellationToken)
    {
        if (imageStorage != null)
        {
            if (await DlcsContext.ImageStorages.AnyAsync(l => l.Id == assetId, cancellationToken))
            {
                DlcsContext.ImageStorages.Attach(imageStorage);
                DlcsContext.Entry(imageStorage).State = EntityState.Modified;
            }
            else
            {
                DlcsContext.ImageStorages.Add(imageStorage);
            }
        }
    }
    
    private async Task<bool> NonBatchedSave(CancellationToken cancellationToken)
    {
        var updatedRows = await DlcsContext.SaveChangesAsync(cancellationToken);
        return updatedRows > 0;
    }

    private async Task<bool> BatchSave(Asset asset, bool ingestFinished, CancellationToken cancellationToken)
    {
        if (!ingestFinished)
        {
            var rowCount = await DlcsContext.SaveChangesAsync(cancellationToken);
            return rowCount > 0;
        }

        var batchAsset = asset.BatchAssets!.Single();
        UpdateBatchAsset(asset, batchAsset);
        var updatedRows = await DlcsContext.SaveChangesAsync(cancellationToken);

        var finishedBatch = await TryFinishBatch(batchAsset.BatchId);
        if (finishedBatch != null)
        {
            updatedRows++;
            await batchCompletedNotificationSender.SendBatchCompletedMessage(finishedBatch, cancellationToken);
        }

        return updatedRows > 0;
    }

    private static void UpdateBatchAsset(Asset asset, BatchAsset batchAsset)
    {
        if (!string.IsNullOrEmpty(asset.Error))
        {
            batchAsset.Status = BatchAssetStatus.Error;
            batchAsset.Error = asset.Error;
        }
        else
        {
            batchAsset.Status = BatchAssetStatus.Completed;
        }
        batchAsset.Finished = DateTime.UtcNow;
    }

    private static void UpdateDeliverable(IDeliverable deliverable, bool ingestFinished)
    {
        if (ingestFinished)
        {
            deliverable.MarkAsFinished();
        }
    }

    private async Task<Batch?> TryFinishBatch(int batchId)
    {
        // Update the "Batches" table, summarising the rows in "BatchAssets"
        var batch = await this.QuerySingleOrDefaultAsync<Batch>(UpdateBatchesSql, new { batchId });

        return batch?.Finished.HasValue ?? false ? batch : null;
    }

    private async Task IncreaseCustomerStorage(ImageStorage imageStorage, CancellationToken cancellationToken)
    {
        try
        {
            await DlcsContext.CustomerStorages
                .Where(cs => cs.Customer == imageStorage.Customer && cs.Space == 0)
                .UpdateFromQueryAsync(cs => new CustomerStorage
                {
                    TotalSizeOfStoredImages = cs.TotalSizeOfStoredImages + imageStorage.Size,
                    TotalSizeOfThumbnails = cs.TotalSizeOfThumbnails + imageStorage.ThumbnailSize
                }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception updating customer storage for {Customer}", imageStorage.Customer);
        }
    }

    private const string UpdateBatchesSql = @"
UPDATE ""Batches"" b
SET ""Completed"" = ba.completed,
    ""Errors""    = ba.errors,
    ""Finished""  = CASE WHEN ba.completed + ba.errors = b.""Count"" THEN now() ELSE null END
FROM (SELECT ""BatchId""                                     as batch_id,
             COUNT(""Status"") filter ( where ""Status"" = 2 ) as errors,
             COUNT(""Status"") filter ( where ""Status"" = 3 ) as completed
      FROM ""BatchAssets""
      GROUP BY ""BatchId"") ba
WHERE b.""Id"" = ba.batch_id
AND b.""Id"" = @batchId
AND b.""Finished"" IS NULL
RETURNING b.*;
";
}
