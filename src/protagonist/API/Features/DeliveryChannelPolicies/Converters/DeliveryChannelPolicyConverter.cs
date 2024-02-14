namespace API.Features.DeliveryChannelPolicies.Converters;

public static class DeliveryChannelPolicyConverter
{
    public static DLCS.HydraModel.DeliveryChannelPolicy ToHydra(
        this DLCS.Model.Policies.DeliveryChannelPolicy deliveryChannelPolicy,
        string baseUrl)
    {
        return new DLCS.HydraModel.DeliveryChannelPolicy(baseUrl)
        {
            Name = deliveryChannelPolicy.Name,
            DisplayName = deliveryChannelPolicy.DisplayName,
            Channel = deliveryChannelPolicy.Channel,
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
            Name = hydraDeliveryChannelPolicy.Name,
            DisplayName = hydraDeliveryChannelPolicy.DisplayName,
            Channel = hydraDeliveryChannelPolicy.Channel,
            PolicyData = hydraDeliveryChannelPolicy.PolicyData,
            Created = hydraDeliveryChannelPolicy.Created.Value, // todo: deal with the nullable values here
            Modified = hydraDeliveryChannelPolicy.Modified.Value 
        };
    }
}