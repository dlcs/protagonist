
namespace API.Features.Storage.Converters;

public static class CustomerStorageConverter
{
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