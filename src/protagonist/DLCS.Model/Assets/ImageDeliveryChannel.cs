#nullable disable

using DLCS.Core.Types;

namespace DLCS.Model.Assets;

public class ImageDeliveryChannel
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; }
    
    /// <summary>
    /// The asset id this policy is assigned to
    /// </summary>
    public AssetId ImageId { get; set; }
    
    /// <summary>
    /// The channel that the policy applies to i.e.: iiif-img
    /// </summary>
    public string Channel { get; set; }
    
    /// <summary>
    /// A string denoting an internal default policy, or a link to a custom policy
    /// </summary>
    public string Policy { get; set; }
}