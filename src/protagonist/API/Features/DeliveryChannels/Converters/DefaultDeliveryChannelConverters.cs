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
    
    /// <summary>
    /// Convert Hydra DefaultDeliveryChannel entity to EF resource
    /// </summary>
    public static DefaultDeliveryChannel ToDlcsModelWithoutPolicy(
        this DLCS.HydraModel.DefaultDeliveryChannel hydraDefaultDeliveryChannel, int space, int customerId)
    {
        return new DefaultDeliveryChannel()
        {
            Id = Guid.TryParse(hydraDefaultDeliveryChannel.Id!, out var defaultDeliveryChannelGuid) 
                ? defaultDeliveryChannelGuid : throw new ArgumentException("Could not parse id into guid"),
            Customer = customerId,
            Space = space,
            MediaType = hydraDefaultDeliveryChannel.MediaType,
        };
    }

    private static string? GetFullyQualifiedPolicyName(DefaultDeliveryChannel defaultDeliveryChannel, string baseUrl)
    {
        return $"{baseUrl}/customers/{defaultDeliveryChannel.Customer}/deliveryChannelPolicies/{defaultDeliveryChannel.DeliveryChannelPolicy.Channel}/{defaultDeliveryChannel.DeliveryChannelPolicy.Name}";
    }
}