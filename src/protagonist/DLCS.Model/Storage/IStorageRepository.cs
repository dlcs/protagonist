using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Storage;

public interface IStorageRepository
{
    public Task<CustomerStorageSummary> GetCustomerStorageSummary(int customerId, CancellationToken cancellationToken);
    public Task<AssetStorageMetric> GetStorageMetrics(int customerId, CancellationToken cancellationToken);
    public Task DecrementAdjunctStorage(int customerId, int space, long adjunctSize, CancellationToken cancellationToken);
}