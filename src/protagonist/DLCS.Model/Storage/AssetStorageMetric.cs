namespace DLCS.Model.Storage;

public record AssetStorageMetric
{
    public CustomerStorage CustomerStorage { get; set; }
    public StoragePolicy Policy { get; set; }
    public long MaximumNumberOfStoredImages => Policy.MaximumNumberOfStoredImages;
    public long CurrentNumberOfStoredImages => CustomerStorage.NumberOfStoredImages;
    public long MaximumTotalSizeOfStoredImages => Policy.MaximumTotalSizeOfStoredImages;
    public long CurrentTotalSizeStoredImages  => CustomerStorage.TotalSizeOfStoredImages;

    /// <summary>
    /// Check if there is allowance to store the specific number of assets.
    /// </summary>
    public bool CanStoreAsset(int assetCount = 1) =>
        CustomerStorage.NumberOfStoredImages + assetCount <= Policy.MaximumNumberOfStoredImages;
}