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
}

public static class KnownDeliveryChannelPolicies
{
    /// <summary>
    /// DeliveryChannelPolicyId for "iiif-img" channel, "default" policy
    /// </summary>
    public const int ImageDefault = 1;
    
    /// <summary>
    /// DeliveryChannelPolicyId for "iiif-img" channel, "use-original" policy
    /// </summary>
    public const int ImageUseOriginal = 2;
    
    /// <summary>
    /// DeliveryChannelPolicyId for "thumbs" channel, "default" policy
    /// </summary>
    public const int ThumbsDefault = 3;
    
    /// <summary>
    /// DeliveryChannelPolicyId for "file" channel, "none" policy
    /// </summary>
    public const int FileNone = 4;
    
    /// <summary>
    /// DeliveryChannelPolicyId for "iiif-av" channel, "default-audio" policy
    /// </summary>
    public const int AvDefaultAudio = 5;
    
    /// <summary>
    /// DeliveryChannelPolicyId for "iiif-av" channel, "default-video" policy
    /// </summary>
    public const int AvDefaultVideo = 6;
    
    /// <summary>
    /// DeliveryChannelPolicyId for "none" channel, "none" policy
    /// </summary>
    public const int None = 7;
}