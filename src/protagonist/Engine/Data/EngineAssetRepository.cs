using System.Data;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;

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
        await using var transaction =
            await dlcsContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var updatedRows = await dlcsContext.SaveChangesAsync(cancellationToken);
            updatedRows += await TryFinishBatch(batchId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return updatedRows > 0;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task UpdateBatch(Asset asset, CancellationToken cancellationToken)
    {
        var batch = await dlcsContext.Batches.FindAsync(new object[] { asset.Batch!.Value },
            cancellationToken: cancellationToken);

        if (batch == null)
        {
            asset.Error = "Unable to find batch associated with image";
            return;
        }

        if (string.IsNullOrEmpty(asset.Error))
        {
            batch.Completed += 1;
        }
        else
        {
            batch.Errors += 1;
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

    private async Task<int> TryFinishBatch(int batchId, CancellationToken cancellationToken) 
        => await dlcsContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Batches\" SET \"Finished\"=now() WHERE \"Id\" = {batchId} and \"Completed\"+\"Errors\"=\"Count\" ",
            cancellationToken);

    private async Task IncreaseCustomerStorage(ImageStorage imageStorage, CancellationToken cancellationToken)
    {
        try
        {
            await dlcsContext.Database.ExecuteSqlInterpolatedAsync(
                $@"
UPDATE ""CustomerStorage"" 
SET     
    ""TotalSizeOfStoredImages""= ""TotalSizeOfStoredImages"" + {imageStorage.Size},
    ""TotalSizeOfThumbnails""= ""TotalSizeOfThumbnails"" + {imageStorage.ThumbnailSize}
WHERE ""Customer"" = {imageStorage.Customer} AND ""Space"" = 0",
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception updating customer storage for {Customer}", imageStorage.Customer);
        }
    }
}