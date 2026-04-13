using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;

namespace DLCS.Model.Storage;

public interface IImageStorageRepository
{
    public Task UpsertImageStorageRecord(ImageStorage? imageStorage, CancellationToken cancellationToken);
}
