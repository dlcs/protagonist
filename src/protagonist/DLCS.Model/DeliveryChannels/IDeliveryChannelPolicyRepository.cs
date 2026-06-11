using System.Threading.Tasks;
using DLCS.Model.Policies;

namespace DLCS.Model.DeliveryChannels;

public interface IDeliveryChannelPolicyRepository
{
    /// <summary>
    /// Retrieves a specific delivery channel policy
    /// </summary>
    /// <param name="customer">The customer to retrieve the policy for</param>
    /// <param name="channel">The channel to retrieve the policy for</param>
    /// <param name="policy">The policy name, or url to retrieve the policy for</param>
    /// <returns>A delivery channel policy</returns>
    public Task<DeliveryChannelPolicy> RetrieveDeliveryChannelPolicy(int customer, string channel, string policy);

    /// <summary>
    /// Retrieves a specific delivery channel policy
    /// </summary>
    /// <param name="customer">The customer to retrieve the policy for</param>
    /// <param name="channel">The channel to retrieve the policy for</param>
    /// <param name="policyId">The id of the policy being retrieved</param>
    /// <returns>A delivery channel policy</returns>
    public Task<DeliveryChannelPolicy> RetrieveDeliveryChannelPolicy(int customer, string channel, int policyId);
}
