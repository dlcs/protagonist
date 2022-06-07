namespace Orchestrator.Infrastructure.ReverseProxy
{
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
        /// Image-server, cluster targeted destination is determined by Proxy:ImageServer value
        /// </summary>
        ImageServer,
        
        /// <summary>
        /// Proxy response from S3
        /// </summary>
        S3
    }
}