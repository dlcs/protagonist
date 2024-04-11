using System;
using DLCS.Core.Types;

namespace DLCS.Model.Assets.Metadata;

public class AssetApplicationMetadata
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// The image id for the attached asset
    /// </summary>
    public AssetId ImageId { get; set; }
    
    public Asset Asset { get; set; }
    
    /// <summary>
    /// Identifier for the type of metadata
    /// </summary>
    public string MetadataType { get; set; }
    
    /// <summary>
    /// JSON object of values for type
    /// </summary>
    public string MetadataValue { get; set; }  
    
    /// <summary>
    /// When the metadata was created. 
    /// </summary>
    public DateTime Created { get; set; }
    
    /// <summary>
    /// When the metadata was last modified.
    /// </summary>
    public DateTime Modified { get; set; }
}