#nullable disable

using System;
using System.Collections.Generic;
using DLCS.Model.Assets;

namespace DLCS.Model.Policies;

public class DeliveryChannelPolicy
{
    /// <summary>
    /// An identifier for the policy
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Identifier for the policy, e.g. "thumbs", "file-pdf" or a GUID etc
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Friendly name for policy
    /// </summary>
    public string DisplayName { get; set; }
    
    /// <summary>
    /// Customer that this policy is for
    /// </summary>
    public int Customer { get; set; }

    /// <summary>
    /// The channel this policy applies to i.e.: iiif-img, iiif-av, etc
    /// </summary>
    public string Channel { get; set; }
    
    /// <summary>
    /// This value is used to determine if this policy is a "System" policy, and thus won't be copied down to a customer
    /// </summary>
    public bool System { get; set; }
    
    /// <summary>
    /// When the policy was created.  When a new customer is created,
    /// the copied polices will have a "Created" date set for the "Modified" time of the parent policy
    /// </summary>
    public DateTime Created { get; set; }
    
    /// <summary>
    /// When the policy was last modified. When a new customer is created,
    /// the copied polices will have a "Modified" date set for the "Modified" time of the parent policy
    /// </summary>
    public DateTime Modified { get; set; }
    
    /// <summary>
    /// The custom policy 
    /// </summary>
    public string PolicyData { get; set; }
    
    /// <summary>
    /// List of delivery channels attached to the image
    /// </summary>
    public virtual List<ImageDeliveryChannel> ImageDeliveryChannels { get; set; }
}