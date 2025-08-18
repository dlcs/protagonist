namespace Thumbs.Settings;

/// <summary>
/// Options to manage configuration of thumbnails
/// </summary>
public class ThumbsSettings
{
    /// <summary>
    /// If true the service will attempt to resize an existing jpg to serve images.
    /// </summary>
    public bool Resize { get; set; }
    
    /// <summary>
    /// If true, smaller thumbnails will be upscaled to handle non-matching requests.
    /// This is ignored if Resize=False
    /// </summary>
    public bool Upscale { get; set; }
    
    /// <summary>
    /// The maximum % size difference for upscaling.
    /// </summary>
    public int UpscaleThreshold { get; set; }
}