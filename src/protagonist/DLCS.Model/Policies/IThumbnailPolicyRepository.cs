using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Policies;

public interface IThumbnailPolicyRepository
{
    /// <summary>
    /// Get a delivery channel thumbs policy for the specified id.
    /// </summary>
    Task<DeliveryChannelPolicy?> GetThumbnailPolicy(int deliveryChannelPolicyId, int customerId,
        CancellationToken cancellationToken = default);
}