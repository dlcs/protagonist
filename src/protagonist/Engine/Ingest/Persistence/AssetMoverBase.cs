using DLCS.Core.Types;
using DLCS.Model.Storage;

namespace Engine.Ingest.Persistence;

/// <summary>
/// Base class with helpers for moving assets to alternative storage
/// </summary>
public abstract class AssetMoverBase
{
    protected readonly IStorageRepository StorageRepository;

    protected AssetMoverBase(IStorageRepository storageRepository)
    {
        StorageRepository = storageRepository;
    }
    
    protected async Task<bool> VerifyFileSize(AssetId assetId, long size)
    {
        var storageMetrics = await StorageRepository.GetStorageMetrics(assetId.Customer, CancellationToken.None);
        var customerHasEnoughSize = storageMetrics.CanStoreAssetSize(size);
        return customerHasEnoughSize;
    }
}