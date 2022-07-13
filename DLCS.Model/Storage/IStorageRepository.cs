using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Storage
{
    public interface IStorageRepository
    {
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

        public Task<CustomerStorageSummary> GetCustomerStorageSummary(int customerId, CancellationToken cancellationToken);
        public Task<ImageCountStorageMetric> GetImageCounts(int customerId, CancellationToken cancellationToken);
    }
}