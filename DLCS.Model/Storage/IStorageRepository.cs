using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Storage
{
    public interface IStorageRepository
    {
        /// <summary>
        /// A named storage policy dictates the maximum number of images and the maximum size on disk they can take up.
        /// </summary>
        /// <param name="id">The name of the policy</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<StoragePolicy?> GetStoragePolicy(string id, CancellationToken cancellationToken);

        /// <summary>
        /// Get an individual CustomerStorage record for a Space.
        /// </summary>
        /// <param name="customerId">The Customer</param>
        /// <param name="spaceId">The Space</param>
        /// <param name="createOnDemand">If true, create a new record if a record doesn't exist; otherwise return null.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<CustomerStorage?> GetCustomerStorage(int customerId, int spaceId, bool createOnDemand,
            CancellationToken cancellationToken);

        public Task<CustomerStorageSummary> GetCustomerStorageSummary(int customerId);
        public Task<ImageCountStorageMetric> GetImageCounts(int putAssetCustomer);
    }
}