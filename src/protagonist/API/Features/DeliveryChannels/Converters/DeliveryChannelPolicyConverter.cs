namespace API.Features.DeliveryChannels.Converters;

public static class DeliveryChannelPolicyConverter
{
    public static DLCS.HydraModel.DeliveryChannelPolicy ToHydra(
        this DLCS.Model.Policies.DeliveryChannelPolicy deliveryChannelPolicy,
        string baseUrl)
    {
        return new DLCS.HydraModel.DeliveryChannelPolicy(baseUrl, deliveryChannelPolicy.Customer,
            deliveryChannelPolicy.Channel, deliveryChannelPolicy.Name)
        {
            DisplayName = deliveryChannelPolicy.DisplayName,
            PolicyData = deliveryChannelPolicy.PolicyData,
            Created = deliveryChannelPolicy.Created,
            Modified = deliveryChannelPolicy.Modified,
        };
    }
    
    public static DLCS.Model.Policies.DeliveryChannelPolicy ToDlcsModel(
        this DLCS.HydraModel.DeliveryChannelPolicy hydraDeliveryChannelPolicy)
    {
        return new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = hydraDeliveryChannelPolicy.CustomerId,
            Name = hydraDeliveryChannelPolicy.Name,
            DisplayName = hydraDeliveryChannelPolicy.DisplayName,
            Channel = hydraDeliveryChannelPolicy.Channel,
            PolicyData = hydraDeliveryChannelPolicy.PolicyData,
            Created = hydraDeliveryChannelPolicy.Created.HasValue // find a better way to deal with these 
                ? hydraDeliveryChannelPolicy.Created.Value
                : DateTime.MinValue,
            Modified = hydraDeliveryChannelPolicy.Modified.HasValue
                ? hydraDeliveryChannelPolicy.Modified.Value
                : DateTime.MinValue,
        };
    }
}