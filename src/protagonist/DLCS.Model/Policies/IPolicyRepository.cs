using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Storage;

namespace DLCS.Model.Policies;

public interface IPolicyRepository : IThumbnailPolicyRepository
{
    /// <summary>
    /// Get ImageOptimisationPolicy with specified Id.
    /// </summary>
    Task<ImageOptimisationPolicy?> GetImageOptimisationPolicy(string imageOptimisationPolicyId, int customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get StoragePolicy with specified Id.
    /// </summary>
    Task<StoragePolicy?> GetStoragePolicy(string storagePolicyId, CancellationToken cancellationToken = default);
}