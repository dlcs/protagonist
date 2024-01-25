#nullable disable

using System;
using DLCS.Model.Policies;

namespace DLCS.Model.DeliveryChannels;

public class DefaultDeliveryChannel
{
    public Guid Id { get; set; }
    
    public int Customer { get; set; }
    
    public int Space { get; set; }
    
    public string MediaType { get; set; }
    
    public int DeliveryChannelPolicyId { get; set; }

    public DeliveryChannelPolicy DeliveryChannelPolicy { get; set; }
}