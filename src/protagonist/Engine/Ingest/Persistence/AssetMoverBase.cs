using DLCS.Core.Types;
using DLCS.Model.Storage;

namespace Engine.Ingest.Persistence;

/// <summary>
/// Base class with helpers for moving assets to alternative storage
/// </summary>
public abstract class AssetMoverBase(IStorageRepository storageRepository)
{
    protected readonly IStorageRepository StorageRepository = storageRepository;

    protected async Task<bool> VerifyFileSize(int customerId, long size, long oldFileSize)
    {
        var storageMetrics = await StorageRepository.GetStorageMetrics(customerId, CancellationToken.None);
        var customerHasEnoughSize = storageMetrics.CanStoreAssetSize(size, oldFileSize);
        return customerHasEnoughSize;
    }
}
