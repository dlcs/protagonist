using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Policies;

public interface IThumbnailPolicyRepository
{
    /// <summary>
    /// Get ThumbnailPolicy with specified Id.
    /// </summary>
    Task<ThumbnailPolicy?> GetThumbnailPolicy(string thumbnailPolicyId, CancellationToken cancellationToken = default);
}