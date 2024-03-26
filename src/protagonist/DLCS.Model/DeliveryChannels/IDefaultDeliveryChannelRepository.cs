using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Policies;

namespace DLCS.Model.DeliveryChannels;

public interface IDefaultDeliveryChannelRepository
{
    /// <summary>
    /// Matches delivery channels based on the media type
    /// </summary>
    /// <param name="mediaType">The media type to match with</param>
    /// <param name="space">The space to check against</param>
    /// <param name="customerId">The customer id</param>
    /// <returns>A list of matched delivery channel policies</returns>
    public Task<List<DeliveryChannelPolicy>> MatchedDeliveryChannels(string mediaType, int space, int customerId);

    /// <summary>
    /// Retrieves a delivery channel policy for a specific channel
    /// </summary>
    /// <param name="mediaType">The media type to match with</param>
    /// <param name="space">The space to check against</param>
    /// <param name="customerId">The customer id</param>
    /// <param name="channel">The channel the policy belongs to</param>
    /// <returns>A matched delivery channel policy, or null when no matches</returns>
    public Task<DeliveryChannelPolicy> MatchDeliveryChannelPolicyForChannel(string mediaType, int space, int customerId,
        string? channel);
}