using System.Threading;
using System.Threading.Tasks;
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
}
