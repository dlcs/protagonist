using DLCS.Model.DeliveryChannels;

namespace API.Features.DeliveryChannels.Converters;

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
    
    /// <summary>
    /// Convert Hydra DefaultDeliveryChannel entity to EF resource
    /// </summary>
    public static DefaultDeliveryChannel ToDlcsModelWithoutPolicy(this DLCS.HydraModel.DefaultDeliveryChannel hydraDefaultDeliveryChannel, int space, int customerId)
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
        return $"{baseUrl}/{defaultDeliveryChannel.Customer}/deliveryChannels/{defaultDeliveryChannel.DeliveryChannelPolicy.Channel}/{defaultDeliveryChannel.DeliveryChannelPolicy.Name}";
    }
}