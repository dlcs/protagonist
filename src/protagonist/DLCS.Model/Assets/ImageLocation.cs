#nullable disable

using DLCS.Core.Types;

namespace DLCS.Model.Assets;

/// <summary>
/// Record indicating where this Image can be found in storage.
/// Only used for "Image" types ('iiif-img' delivery channel) 
/// </summary>
public class ImageLocation
{
    /// <summary>
    /// The Id of asset
    /// </summary>
    public AssetId Id { get; set; }
    
    /// <summary>
    /// The s3:// URI where this the image-server source file can be found
    /// </summary>
    public string S3 { get; set; }
    
    /// <summary>
    /// Currently unused
    /// </summary>
    public string Nas { get; set; }
}
