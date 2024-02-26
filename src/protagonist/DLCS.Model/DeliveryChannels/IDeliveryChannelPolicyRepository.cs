using DLCS.Model.Policies;

namespace DLCS.Model.DeliveryChannels;

public interface IDeliveryChannelPolicyRepository
{
    public DeliveryChannelPolicy RetrieveDeliveryChannelPolicy(int customer, string channel, string policy);
}