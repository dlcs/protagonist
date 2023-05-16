using System.Data;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Engine.Data;

public class EngineAssetRepository : IEngineAssetRepository
{
    private readonly DlcsContext dlcsContext;
    private readonly ILogger<EngineAssetRepository> logger;

    public EngineAssetRepository(DlcsContext dlcsContext, ILogger<EngineAssetRepository> logger)
    {
        this.dlcsContext = dlcsContext;
        this.logger = logger;
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

            var success = hasBatch
                ? await BatchSave(asset.Batch!.Value, cancellationToken)
                : await NonBatchedSave(cancellationToken);

            if (success && imageStorage != null)
            {
                await IncreaseCustomerStorage(imageStorage, cancellationToken);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finalising Asset {AssetId} in DB", asset.Id);
            return false;
        }
    }

    public ValueTask<Asset?> GetAsset(AssetId assetId, CancellationToken cancellationToken = default)
        => dlcsContext.Images.FindAsync(new object[] { assetId }, cancellationToken);

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
            updatedRows += await TryFinishBatch(batchId, cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
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

        // If the asset is tracked then no need to attach + set modified properties
        // Assets will be tracked when finalising a Timebased ingest as the Asset will have been read from context
        if (dlcsContext.Images.Local.Any(a => a.Id == asset.Id)) return;
        
        dlcsContext.Images.Attach(asset);
        var entry = dlcsContext.Entry(asset);
        entry.Property(p => p.Width).IsModified = true;
        entry.Property(p => p.Height).IsModified = true;
        entry.Property(p => p.Duration).IsModified = true;
        entry.Property(p => p.Error).IsModified = true;
        entry.Property(p => p.Ingesting).IsModified = true;
        entry.Property(p => p.Finished).IsModified = true;

        if (asset.MediaType.HasText() && asset.MediaType != "unknown")
        {
            entry.Property(p => p.MediaType).IsModified = true;
        }
    }

    private Task<int> TryFinishBatch(int batchId, CancellationToken cancellationToken)
        => dlcsContext.Batches
            .Where(b => b.Id == batchId && b.Count == b.Completed + b.Errors)
            .UpdateFromQueryAsync(b => new Batch { Finished = DateTime.UtcNow }, cancellationToken);

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