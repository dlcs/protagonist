using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Storage;

public static class ImageStorageX
{
    /// <summary>
    /// Upsert ImageStorage record, note that only update Size, ThumbnailSize and LastChecked.
    /// See AdjustAdjunctSize for altering the AdjunctSize
    /// </summary>
    public static async Task UpsertImageStorageRecord(this DbSet<ImageStorage> imageStorages,
        ImageStorage? imageStorage, CancellationToken cancellationToken)
    {
        if (imageStorage == null) return;

        var existing = await imageStorages.SingleOrDefaultAsync(l => l.Id == imageStorage.Id, cancellationToken);
        if (existing == null)
        {
            imageStorages.Add(imageStorage);
        }
        else
        {
            // AdjunctSize is a running tally maintained by AdjustAdjunctSize - asset ingest must not overwrite it
            existing.Size = imageStorage.Size;
            existing.ThumbnailSize = imageStorage.ThumbnailSize;
            existing.LastChecked = imageStorage.LastChecked;
        }
    }

    /// <summary>
    /// Apply a signed delta to the adjunct size tally for specified asset (clamped at zero).
    /// </summary>
    public static Task AdjustAdjunctSize(this DbSet<ImageStorage> imageStorages, AssetId assetId,
        long sizeDelta, CancellationToken cancellationToken) =>
        sizeDelta == 0
            ? Task.CompletedTask
            : imageStorages
                .Where(s => s.Id == assetId)
                .UpdateFromQueryAsync(s => new ImageStorage
                {
                    AdjunctSize = s.AdjunctSize + sizeDelta > 0 ? s.AdjunctSize + sizeDelta : 0
                }, cancellationToken);
}
