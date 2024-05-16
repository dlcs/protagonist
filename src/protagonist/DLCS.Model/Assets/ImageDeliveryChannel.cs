#nullable disable

using System;
using System.Collections.Generic;
using DLCS.Core.Types;
using DLCS.Model.Policies;
namespace DLCS.Model.Assets;

public class ImageDeliveryChannel
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// The image id for the attached asset
    /// </summary>
    public AssetId ImageId { get; set; }

    /// <summary>
    /// The channel this policy applies to
    /// </summary>
    public string Channel { get; set; }
    
    public DeliveryChannelPolicy DeliveryChannelPolicy { get; set; }
    
    /// <summary>
    /// The delivery channel policy id for the attached delivery channel policy
    /// </summary>
    public int DeliveryChannelPolicyId { get; set; }
}