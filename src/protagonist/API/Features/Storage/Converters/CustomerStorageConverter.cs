namespace API.Features.Storage.Converters;

/// <summary>
/// Conversion between API and EF forms of CustomerStorage resource
/// </summary>
public static class CustomerStorageConverter
{
    /// <summary>
    /// Convert CustomerStorage entity to API resource
    /// </summary>
    public static DLCS.HydraModel.CustomerStorage ToHydra(this DLCS.Model.Storage.CustomerStorage customerStorage, string baseUrl)
    {
        var hydraCustomerStorage = new DLCS.HydraModel.CustomerStorage(baseUrl, customerStorage.Customer, customerStorage.Space)
        {
            StoragePolicy = customerStorage.StoragePolicy,
            NumberOfStoredImages = customerStorage.NumberOfStoredImages,
            TotalSizeOfStoredImages = customerStorage.TotalSizeOfStoredImages,
            TotalSizeOfThumbnails = customerStorage.TotalSizeOfThumbnails,
            LastCalculated = customerStorage.LastCalculated
        };
        return hydraCustomerStorage;
    }
}