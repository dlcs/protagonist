using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Storage;

public class ImageStorageRepository(DlcsContext dlcsContext) : IImageStorageRepository
{
    public async Task UpsertImageStorageRecord(ImageStorage? imageStorage, CancellationToken cancellationToken)
    {
        if (imageStorage != null)
        {
            if (await dlcsContext.ImageStorages.AnyAsync(l => l.Id == imageStorage.Id, cancellationToken))
            {
                dlcsContext.ImageStorages.Attach(imageStorage);
                dlcsContext.Entry(imageStorage).State = EntityState.Modified;
            }
            else
            {
                dlcsContext.ImageStorages.Add(imageStorage);
            }
        }
    }
}
