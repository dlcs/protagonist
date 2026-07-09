using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Model.Storage;

public interface IStorageRepository
{
    public Task<CustomerStorageSummary> GetCustomerStorageSummary(int customerId, CancellationToken cancellationToken);
    public Task<AssetStorageMetric> GetStorageMetrics(int customerId, CancellationToken cancellationToken);

    /// <summary>Decrement adjunct counts in CustomerStorage and AdjunctSize in ImageStorage.</summary>
    public Task DecrementAdjunctStorage(AssetId assetId, long adjunctSize, CancellationToken cancellationToken);

    /// <summary>Decrement adjunct counts in CustomerStorage and AdjunctSize in ImageStorage when there are multiple adjuncts being removed.</summary>
    public Task DecrementAdjunctStorage(AssetId assetId, long adjunctSize, int adjunctCount, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically apply a signed size delta to the stored-adjunct size totals: <c>TotalSizeOfStoredAdjuncts</c> in
    /// CustomerStorage and <c>AdjunctSize</c> in ImageStorage (both clamped at zero). Does not change adjunct counts.
    /// </summary>
    /// <remarks>Use for adjunct ingest, where the delta may be negative (e.g. a hosted adjunct moving to an optimised origin).</remarks>
    public Task AdjustAdjunctStoredSize(AssetId assetId, long sizeDelta, CancellationToken cancellationToken);
    
    /// <summary>
    /// Delete customer storage record
    /// </summary>
    public Task<bool> DeleteCustomerStorage(int customer, int space, CancellationToken cancellationToken);

    /// <summary>
    /// Create new customer storage record for given space
    /// </summary>
    public Task TryCreateCustomerStorage(int customer, int? space, string policy = StoragePolicy.DefaultStoragePolicyName,
        CancellationToken cancellationToken = default);
}
