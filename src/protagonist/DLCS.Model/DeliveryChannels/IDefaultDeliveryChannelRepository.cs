using System.Collections.Generic;
using DLCS.Model.Policies;

namespace DLCS.Model.DeliveryChannels;

public interface IDefaultDeliveryChannelRepository
{
    /// <summary>
    /// Gets the default delivery channels that belong to a customer and space
    /// </summary>
    /// <param name="customer">The customer to retrieve for</param>
    /// <param name="space">The space to retrieve for</param>
    /// <returns>A list of default delivery channels</returns>
    public List<DefaultDeliveryChannel> GetDefaultDeliveryChannelsForCustomer(int customer, int space);

    /// <summary>
    /// Matches delivery channels based on the media type
    /// </summary>
    /// <param name="mediaType">The media type to match with</param>
    /// <param name="space">The space to check against</param>
    /// <param name="customerId">The customer id</param>
    /// <returns>A list of matched delivery channel policies</returns>
    public List<DeliveryChannelPolicy> MatchedDeliveryChannels(string mediaType, int space, int customerId);

    /// <summary>
    /// Retrieves a delivery channel policy for a specific channel
    /// </summary>
    /// <param name="mediaType">The media type to match with</param>
    /// <param name="space">The space to check against</param>
    /// <param name="customerId">The customer id</param>
    /// <param name="channel">The channel the policy belongs to</param>
    /// <returns>A matched deliovery channel policy, or null when no matches</returns>
    DeliveryChannelPolicy MatchDeliveryChannelPolicyForChannel(string mediaType, int space, int customerId, string? channel);
}