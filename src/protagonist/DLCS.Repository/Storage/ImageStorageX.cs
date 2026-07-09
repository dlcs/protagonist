using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Storage;

public static class ImageStorageX
{
    public static async Task UpsertImageStorageRecord(this DbSet<ImageStorage> imageStorages, ImageStorage? imageStorage, CancellationToken cancellationToken)
    {
        if (imageStorage != null)
        {
            if (await imageStorages.AnyAsync(l => l.Id == imageStorage.Id, cancellationToken))
            {
                imageStorages.Update(imageStorage);
            }
            else
            {
                imageStorages.Add(imageStorage);
            }
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
