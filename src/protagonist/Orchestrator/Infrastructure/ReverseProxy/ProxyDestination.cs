namespace Orchestrator.Infrastructure.ReverseProxy;

/// <summary>
/// Enum representing potential locations to proxy to
/// </summary>
public enum ProxyDestination
{
    /// <summary>
    /// Unknown, fallback value
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Orchestrator for handling standard requests (legacy, will be removed)
    /// </summary>
    Orchestrator,
    
    /// <summary>
    /// Thumbs services, for handling requests for thumbs
    /// </summary>
    Thumbs,
    
    /// <summary>
    /// Resize thumbs services, for handling requests for thumbs that are resized from pre-generated version
    /// </summary>
    ResizeThumbs,
    
    /// <summary>
    /// Image-server, cluster target destination is determined by ImageServer value
    /// </summary>
    ImageServer,
    
    /// <summary>
    /// Specially configured image-server to handle /full/ image requests without the need to orchestrate
    /// </summary>
    SpecialServer,
    
    /// <summary>
    /// Proxy response from S3
    /// </summary>
    S3
}