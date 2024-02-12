using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Policies;

namespace DLCS.Model.DeliveryChannels;

public interface IDeliveryChannelPolicyRepository
{
    /// <summary>
    /// Retrieves a specific delivery channel policy
    /// </summary>
    /// <param name="customerId">The id of the customer used to retrieve a policy</param>
    /// <param name="policyName">The name of the policy to retrieve</param>
    /// <param name="channel">The channel of the policy to retrieve</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A delivery channel policy</returns>
    public Task<DeliveryChannelPolicy?> GetDeliveryChannelPolicy(int customerId, string policyName, string channel,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Adds delivery channel policies to the table for a customer
    /// </summary>
    /// <param name="customerId">The customer id to create the policies for</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>Whether creating the new policies was successful or not</returns>
    public Task<bool> AddDeliveryChannelCustomerPolicies(int customerId,
        CancellationToken cancellationToken);
}