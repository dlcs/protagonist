using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Guard;
using DLCS.Model.Assets;
using IIIF.ImageApi;
using Newtonsoft.Json;

namespace DLCS.Model.Policies;

public static class DeliveryChannelPolicyX
{
    /// <summary>
    /// Get PolicyData as a list of IIIF <see cref="SizeParameter"/> objects
    /// </summary>
    /// <param name="deliveryChannelPolicy">Current <see cref="DeliveryChannelPolicy"/></param>
    /// <returns>Collection of <see cref="SizeParameter"/> objects</returns>
    /// <exception cref="InvalidOperationException">Thrown if specified policy is not for thumbs channel</exception>
    public static List<SizeParameter> ThumbsDataAsSizeParameters(this DeliveryChannelPolicy deliveryChannelPolicy)
    {
        if (deliveryChannelPolicy.Channel != AssetDeliveryChannels.Thumbnails)
        {
            throw new InvalidOperationException("Policy is not for thumbs channel");
        }
        var thumbSizes = deliveryChannelPolicy.PolicyDataAs<List<string>>();
        return thumbSizes
            .ThrowIfNull(nameof(thumbSizes))
            .Select(s => SizeParameter.Parse(s))
            .ToList();
    }
    
    /// <summary>
    /// Get timebased PolicyData as a list of strings
    /// </summary>
    /// <param name="deliveryChannelPolicy">Current <see cref="DeliveryChannelPolicy"/></param>
    /// <returns>Collection of strings representing timebased policies</returns>
    /// <exception cref="InvalidOperationException">Thrown if specified policy is not for thumbs channel</exception>
    public static List<string> AsTimebasedPresets(this DeliveryChannelPolicy deliveryChannelPolicy)
    {
        if (deliveryChannelPolicy.Channel != AssetDeliveryChannels.Timebased)
        {
            throw new InvalidOperationException("Policy is not for timebased channel");
        }

        var timeBasedPresets = deliveryChannelPolicy.PolicyDataAs<List<string>>();
        
        return timeBasedPresets.ThrowIfNull(nameof(timeBasedPresets));
    }
    
    /// <summary>
    /// Deserialise PolicyData as specified type
    /// </summary>
    public static T? PolicyDataAs<T>(this DeliveryChannelPolicy deliveryChannelPolicy)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(deliveryChannelPolicy.PolicyData);
        }
        catch (JsonSerializationException ex)
        {
            throw new InvalidOperationException($"Unable to deserialize policyData to {typeof(T).Name}", ex);
        }
    }
}