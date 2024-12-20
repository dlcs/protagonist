using System.Data;
using DLCS.AWS.SNS.Messaging;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Engine.Data;

public class EngineAssetRepository : IEngineAssetRepository
{
    private readonly DlcsContext dlcsContext;
    private readonly ILogger<EngineAssetRepository> logger;
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
        var hasBatch = (asset.Batch ?? 0) != 0;

        logger.LogDebug("Updating ingested asset {AssetId}. HasBatch:{HasBatch}, Finished:{Finished}", asset.Id,
            hasBatch, ingestFinished);

        try
        {
            // Update Batch first as this might set the Error property on Asset
            if (hasBatch && ingestFinished) 
            {
                await UpdateBatch(asset, cancellationToken);
            }

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
            
            var updatedRows = hasBatch
                ? await BatchSave(asset.Batch!.Value, cancellationToken)
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

    public ValueTask<Asset?> GetAsset(AssetId assetId, CancellationToken cancellationToken = default)
        => new(dlcsContext.Images.IncludeDeliveryChannelsWithPolicy()
            .SingleOrDefaultAsync(i => i.Id == assetId, cancellationToken));

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

    private async Task<bool> BatchSave(int batchId, CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = null;
        if (dlcsContext.Database.CurrentTransaction == null)
        {
            transaction =
                await dlcsContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        }

        try
        {
            var updatedRows = await dlcsContext.SaveChangesAsync(cancellationToken);
            var finishedBatch = await TryFinishBatch(batchId, cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            if (finishedBatch != null)
            {
                updatedRows++;
                await batchCompletedNotificationSender.SendBatchCompletedMessage(finishedBatch,
                    cancellationToken);
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

    private async Task UpdateBatch(Asset asset, CancellationToken cancellationToken)
    {
        int rowsUpdated;
        
        var queryable = dlcsContext.Batches.Where(b => b.Id == asset.Batch!.Value);
        if (string.IsNullOrEmpty(asset.Error))
        {
            rowsUpdated = await queryable
                .UpdateFromQueryAsync(b => new Batch { Completed = b.Completed + 1 }, cancellationToken);
        }
        else
        {
            rowsUpdated = await queryable
                .UpdateFromQueryAsync(b => new Batch { Errors = b.Errors + 1 }, cancellationToken);
        }
        
        if (rowsUpdated == 0)
        {
            asset.Error = "Unable to update batch associated with image";
        }
    }

    private void UpdateAsset(Asset asset, bool ingestFinished)
    {
        if (ingestFinished)
        {
            asset.MarkAsFinished();
        }
    }

    private async Task<Batch?> TryFinishBatch(int batchId,
        CancellationToken cancellationToken)
    {
        var rowsAffected = await dlcsContext.Batches
            .Where(b => b.Id == batchId && b.Count == b.Completed + b.Errors)
            .UpdateFromQueryAsync(b => new Batch { Finished = DateTime.UtcNow }, cancellationToken);

        return rowsAffected == 0 
            ? null
            : await dlcsContext.Batches.FindAsync(new object[] { batchId }, cancellationToken);
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
}