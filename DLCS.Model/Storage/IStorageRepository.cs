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

        /// <summary>
        /// Verify that the proposed new file size will not exceed storage policy limits.
        /// </summary>
        /// <param name="customer">Customer Identifier</param>
        /// <param name="proposedNewFileSize">The size, in bytes, of new asset.</param>
        /// <returns>True if storage allowed, else false.</returns>
        Task<bool> VerifyStoragePolicyBySize(int customer, long proposedNewFileSize,
            CancellationToken cancellationToken = default);

        public Task<CustomerStorageSummary> GetCustomerStorageSummary(int customerId, CancellationToken cancellationToken);
        public Task<ImageCountStorageMetric> GetImageCounts(int customerId, CancellationToken cancellationToken);
    }
}