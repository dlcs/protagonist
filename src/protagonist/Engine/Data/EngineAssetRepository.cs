using System.Data;
using DLCS.AWS.SNS.Messaging;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Engine.Data;

public class EngineAssetRepository : IEngineAssetRepository
{
    private readonly ILogger<EngineAssetRepository> logger;
    private readonly DlcsContext dlcsContext;
    private readonly IBatchCompletedNotificationSender batchCompletedNotificationSender;

    public EngineAssetRepository(
        DlcsContext dlcsContext, 
        IBatchCompletedNotificationSender batchCompletedNotificationSender, 
        ILogger<EngineAssetRepository> logger)
    {
        this.dlcsContext = dlcsContext;
        this.logger = logger;
        this.batchCompletedNotificationSender = batchCompletedNotificationSender;
    }

    public async Task<bool> UpdateIngestedAsset(Asset asset, ImageLocation? imageLocation, ImageStorage? imageStorage,
        bool ingestFinished, CancellationToken cancellationToken = default)
    {
        var hasBatch = !asset.BatchAssets.IsNullOrEmpty();

        logger.LogDebug("Updating ingested asset {AssetId}. HasBatch:{HasBatch}, Finished:{Finished}", asset.Id,
            hasBatch, ingestFinished);

        try
        {
            // Update Batch first as this might set the Error property on Asset
            /*if (hasBatch && ingestFinished) 
            {
                await UpdateBatch(asset, asset.BatchAssets!.Single(), cancellationToken);
            }*/

            UpdateAsset(asset, ingestFinished);

            if (imageLocation != null)
            {
                if (await dlcsContext.ImageLocations.AnyAsync(l => l.Id == asset.Id, cancellationToken))
                {
                    dlcsContext.ImageLocations.Attach(imageLocation);
                    dlcsContext.Entry(imageLocation).State = EntityState.Modified;
                }
                else
                {
                    dlcsContext.ImageLocations.Add(imageLocation);
                }
            }

            if (imageStorage != null)
            {
                if (await dlcsContext.ImageStorages.AnyAsync(l => l.Id == asset.Id, cancellationToken))
                {
                    dlcsContext.ImageStorages.Attach(imageStorage);
                    dlcsContext.Entry(imageStorage).State = EntityState.Modified;
                }
                else
                {
                    dlcsContext.ImageStorages.Add(imageStorage);
                }
            }
            
            // TODO - this needs to take into account whether the image is completed or not
            var updatedRows = hasBatch
                ? await BatchSave(asset, ingestFinished, cancellationToken)
                : await NonBatchedSave(cancellationToken);

            if (updatedRows && imageStorage != null)
            {
                await IncreaseCustomerStorage(imageStorage, cancellationToken);
            }
            
            return updatedRows || !ingestFinished; // if the ingest hasn't finished, rows can be not updated - meaning success
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finalising Asset {AssetId} in DB", asset.Id);
            return false;
        }
    }

    public ValueTask<Asset?> GetAsset(AssetId assetId, int? batchId, CancellationToken cancellationToken = default)
    {
        var images = dlcsContext.Images.IncludeDeliveryChannelsWithPolicy();

        if (batchId.HasValue)
        {
            images = images.Include(i => i.BatchAssets.Single(ba => ba.BatchId == batchId.Value));
        }
        
        return new ValueTask<Asset?>(images.SingleOrDefaultAsync(i => i.Id == assetId, cancellationToken));
    }

    public async Task<long?> GetImageSize(AssetId assetId, CancellationToken cancellationToken = default)
    {
        var imageSize = await dlcsContext.ImageStorages.AsNoTracking()
            .Where(i => i.Id == assetId)
            .Select(i => i.Size)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        return imageSize;
    }
    
    private async Task<bool> NonBatchedSave(CancellationToken cancellationToken)
    {
        var updatedRows = await dlcsContext.SaveChangesAsync(cancellationToken);
        return updatedRows > 0;
    }

    private async Task<bool> BatchSave(Asset asset, bool ingestFinished, CancellationToken cancellationToken)
    {
        if (!ingestFinished) return true;
        
        IDbContextTransaction? transaction = null;
        if (dlcsContext.Database.CurrentTransaction == null)
        {
            transaction =
                await dlcsContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        }

        try
        {
            var batchAsset = asset.BatchAssets!.First();
            UpdateBatchAsset(asset, batchAsset);
            var updatedRows = await dlcsContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            var finishedBatch = await TryFinishBatch(batchAsset.BatchId, cancellationToken);
            if (finishedBatch != null)
            {
                updatedRows++;
                await batchCompletedNotificationSender.SendBatchCompletedMessage(finishedBatch, cancellationToken);
            }

            return updatedRows > 0;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
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

    private void UpdateAsset(Asset asset, bool ingestFinished)
    {
        if (ingestFinished)
        {
            asset.MarkAsFinished();
        }
    }

    private async Task<Batch?> TryFinishBatch(int batchId, CancellationToken cancellationToken)
    {
        // Update the "Batches" table, summarising the rows in "BatchAssets"
        var batchParameter = new NpgsqlParameter("batch", NpgsqlDbType.Integer) { Value = batchId };
        var asNoTracking = dlcsContext.Batches
            .FromSqlRaw(UpdateBatchesSql, batchParameter)
            .AsNoTracking();
        var batch = await asNoTracking
            .SingleOrDefaultAsync(cancellationToken);

        return batch?.Finished.HasValue ?? false ? batch : null;
    }

    private async Task IncreaseCustomerStorage(ImageStorage imageStorage, CancellationToken cancellationToken)
    {
        try
        {
            await dlcsContext.CustomerStorages
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
AND b.""Id"" = @batch
RETURNING b.*;
";
}