using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Storage;

namespace DLCS.Model.Policies;

public interface IPolicyRepository
{
    /// <summary>
    /// Get ThumbnailPolicy with specified Id.
    /// </summary>
    Task<ThumbnailPolicy?> GetThumbnailPolicy(string thumbnailPolicyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get ImageOptimisationPolicy with specified Id.
    /// </summary>
    Task<ImageOptimisationPolicy?> GetImageOptimisationPolicy(string imageOptimisationPolicyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get StoragePolicy with specified Id.
    /// </summary>
    Task<StoragePolicy?> GetStoragePolicy(string storagePolicyId, CancellationToken cancellationToken = default);
}