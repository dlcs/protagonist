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
}
