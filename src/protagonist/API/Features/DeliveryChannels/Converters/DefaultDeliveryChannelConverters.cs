using DLCS.Model.DeliveryChannels;

namespace API.Features.DeliveryChannels.Converters;

public static class DefaultDeliveryChannelConverters
{
    /// <summary>
    /// Convert DefaultDeliveryChannel entity to API resource
    /// </summary>
    public static DLCS.HydraModel.DefaultDeliveryChannel ToHydra(this DefaultDeliveryChannel defaultDeliveryChannel, string baseUrl)
    {
        var policy = defaultDeliveryChannel.DeliveryChannelPolicy.System
            ? defaultDeliveryChannel.DeliveryChannelPolicy.Name
            : GetFullyQualifiedPolicyName(defaultDeliveryChannel, baseUrl);

        return new DLCS.HydraModel.DefaultDeliveryChannel(baseUrl, defaultDeliveryChannel.Customer,
            defaultDeliveryChannel.DeliveryChannelPolicy.Channel, policy, defaultDeliveryChannel.MediaType,
            defaultDeliveryChannel.Id.ToString(), defaultDeliveryChannel.Space);
    }

    private static string? GetFullyQualifiedPolicyName(DefaultDeliveryChannel defaultDeliveryChannel, string baseUrl)
    {
        return $"{baseUrl}/customers/{defaultDeliveryChannel.Customer}/deliveryChannelPolicies/{defaultDeliveryChannel.DeliveryChannelPolicy.Channel}/{defaultDeliveryChannel.DeliveryChannelPolicy.Name}";
    }
}