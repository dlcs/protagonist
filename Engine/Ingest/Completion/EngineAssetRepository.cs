using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;

namespace Engine.Ingest.Completion;

public interface IEngineAssetRepository
{
    /// <summary>
    /// Update database with ingested asset.
    /// </summary>
    /// <param name="asset">Asset to update</param>
    /// <param name="imageLocation">ImageLocation, optional as may have exited prior to creation</param>
    /// <param name="imageStorage">ImageStorage, optional as may have exited prior to creation</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>True if successful</returns>
    Task<bool> UpdateIngestedAsset(Asset asset, ImageLocation? imageLocation, ImageStorage? imageStorage,
        CancellationToken cancellationToken = default);
}

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
        CancellationToken cancellationToken = default)
    {
        var hasBatch = (asset.Batch ?? 0) != 0;

        try
        {
            // Update Batch first as this might set the Error property on Asset
            if (hasBatch)
            {
                await UpdateBatch(asset, cancellationToken);
            }

            UpdateAsset(asset);

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

            return hasBatch
                ? await BatchSave(asset.Batch!.Value, cancellationToken)
                : await NonBatchedSave(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finalising Asset {AssetId} in DB", asset.Id);
            return false;
        }
    }

    private async Task<bool> NonBatchedSave(CancellationToken cancellationToken)
    {
        var updatedRows = await dlcsContext.SaveChangesAsync(cancellationToken);
        return updatedRows > 0;
    }

    private async Task<bool> BatchSave(int batchId, CancellationToken cancellationToken)
    {
        await using var transaction = await dlcsContext.Database.BeginTransactionAsync(cancellationToken);

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

    private void UpdateAsset(Asset asset)
    {
        asset.Ingesting = false;
        asset.Finished = DateTime.UtcNow;
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
}