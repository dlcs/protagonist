using DLCS.Model.DeliveryChannels;

namespace API.Features.DefaultDeliveryChannels.Converters;

public static class DefaultDeliveryChannelConverters
{
    /// <summary>
    /// Convert DefaultDeliveryChannel entity to API resource
    /// </summary>
    public static DLCS.HydraModel.DefaultDeliveryChannel ToHydra(this DefaultDeliveryChannel defaultDeliveryChannel, string baseUrl)
    {
        var hydra = new DLCS.HydraModel.DefaultDeliveryChannel()
        {   
            Policy = defaultDeliveryChannel.DeliveryChannelPolicy.System ? defaultDeliveryChannel.DeliveryChannelPolicy.Name : GetFullyQualifiedPolicyName(defaultDeliveryChannel, baseUrl),
            Channel = defaultDeliveryChannel.DeliveryChannelPolicy.Channel,
            MediaType = defaultDeliveryChannel.MediaType,
            Id = defaultDeliveryChannel.Id.ToString() //todo: change this to a URL
        };
            
        return hydra;
    }

    private static string? GetFullyQualifiedPolicyName(DefaultDeliveryChannel defaultDeliveryChannel, string baseUrl)
    {
        return $"{baseUrl}/{defaultDeliveryChannel.Customer}/deliveryChannels/{defaultDeliveryChannel.DeliveryChannelPolicy.Channel}/{defaultDeliveryChannel.DeliveryChannelPolicy.Name}";
    }
}