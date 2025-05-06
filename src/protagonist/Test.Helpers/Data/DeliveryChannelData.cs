using System.Collections.Generic;
using System.Linq;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using DLCS.Repository;

namespace Test.Helpers.Data;

public static class DeliveryChannelData
{
    /// <summary>
    /// Helper function to get default delivery channel and default thumbs policy for given customer
    /// </summary>
    public static List<ImageDeliveryChannel> GetImageDeliveryChannels(this DlcsContext dlcsContext, int customer = 99)
    {
        var thumbsPolicy = dlcsContext.DeliveryChannelPolicies
            .Single(d => d.Channel == AssetDeliveryChannels.Thumbnails && d.Customer == customer);

        return
        [
            new ImageDeliveryChannel
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault,
            },

            new ImageDeliveryChannel
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                DeliveryChannelPolicyId = thumbsPolicy.Id,
            }
        ];
    }

    /// <summary>
    /// Return <see cref="ImageDeliveryChannel"/> without policyId from provided comma delimited list of channels
    /// </summary>
    public static List<ImageDeliveryChannel> GenerateDeliveryChannels(this string csvDeliveryChannels)
        => csvDeliveryChannels
            .Split(",")
            .Select(deliveryChannel => new ImageDeliveryChannel { Channel = deliveryChannel })
            .ToList();
}
