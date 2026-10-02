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
            // Policies exist at customer level only; a space-level row's policy value
            // just echoes the customer's and is neither editable nor enforced per space
            StoragePolicy = customerStorage.Space == null
                ? $"{baseUrl}/storagePolicies/{customerStorage.StoragePolicy}"
                : null,
            NumberOfStoredImages = customerStorage.NumberOfStoredImages,
            TotalSizeOfStoredImages = customerStorage.TotalSizeOfStoredImages,
            TotalSizeOfThumbnails = customerStorage.TotalSizeOfThumbnails,
            NumberOfStoredAdjuncts = customerStorage.NumberOfStoredAdjuncts,
            TotalSizeOfStoredAdjuncts = customerStorage.TotalSizeOfStoredAdjuncts,
            LastCalculated = customerStorage.LastCalculated
        };
        
        return hydraCustomerStorage;
    }
}