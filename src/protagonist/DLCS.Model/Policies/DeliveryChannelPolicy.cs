#nullable disable

using System;
using System.Collections.Generic;
using DLCS.Model.Assets;

namespace DLCS.Model.Policies;

public class DeliveryChannelPolicy
{
    /// <summary>
    /// Identifier for the policy, e.g. "thumbs", "file-pdf" or a GUId etc
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// The name of the policy
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
    /// Space that this policy is for
    /// </summary>
    public int Space { get; set; }
    
    /// <summary>
    /// The channel this policy applies to i.e.: iiif-img, iiif-av, etc
    /// </summary>
    public string Channel { get; set; }
    
    /// <summary>
    /// A wildcard string used to help match against a media type
    /// </summary>
    public string MediaType { get; set; }
    
    /// <summary>
    /// When the policy was created
    /// </summary>
    public DateTime PolicyCreated { get; set; }
    
    /// <summary>
    /// When the policy was last modified
    /// </summary>
    public DateTime PolicyModified { get; set; }
    
    /// <summary>
    /// The custom policy 
    /// </summary>
    public string PolicyData { get; set; }
    
    /// <summary>
    /// List of delivery channels attached to the image
    /// </summary>
    public virtual List<ImageDeliveryChannel> ImageDeliveryChannels { get; set; }
}