using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Policies;

namespace DLCS.Model.DeliveryChannels;

public interface IDeliveryChannelPolicyRepository
{
    public Task<DeliveryChannelPolicy?> GetDeliveryChannelPolicy(int customerId, string policyName, string channel,
        CancellationToken cancellationToken);
    
    public Task<bool> AddDeliveryChannelCustomerPolicies(int customerId,
        CancellationToken cancellationToken);
}