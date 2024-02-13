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
            PolicyModified = deliveryChannelPolicy.Modified,
        };
    }
}