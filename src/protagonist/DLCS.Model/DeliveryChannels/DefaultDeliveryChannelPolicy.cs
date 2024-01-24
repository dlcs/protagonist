#nullable disable

using DLCS.Model.Policies;

namespace DLCS.Model.DeliveryChannels;

public class DefaultDeliveryChannelPolicy
{
    public int Id { get; set; }
    
    public int Customer { get; set; }
    
    public int Space { get; set; }
    
    public string MediaType { get; set; }
    
    public int DeliveryChannelPolicyId { get; set; }

    public DeliveryChannelPolicy DeliveryChannelPolicy { get; set; }
}