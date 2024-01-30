#nullable disable

using System;
using DLCS.Model.Policies;

namespace DLCS.Model.DeliveryChannels;

public class DefaultDeliveryChannel
{
    /// <summary>
    /// A GUID used as an identifier for this entry in the table
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// The customer this delivery channel will be applied to
    /// </summary>
    public int Customer { get; set; }
    
    /// <summary>
    /// The space this delivery channel will be applied to
    /// </summary>
    public int Space { get; set; }
    
    /// <summary>
    /// The media type this policy will apply to.  This value can use wildcards
    /// </summary>
    public string MediaType { get; set; }
    
    /// <summary>
    /// The related delivery channel policy
    /// </summary>
    public int DeliveryChannelPolicyId { get; set; }

    public DeliveryChannelPolicy DeliveryChannelPolicy { get; set; }
}