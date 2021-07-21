namespace Orchestrator.ReverseProxy
{
    /// <summary>
    /// Record that represents how a request should be proxied
    /// </summary>
    /// <param name="Target">Downstream system to Proxy to</param>
    /// <param name="Path">Path to proxy to, if rewritten</param>
    public record ProxyAction(ProxyTo Target, string? Path = null)
    {
        /// <summary>
        /// Get value indicating whether action has a path
        /// </summary>
        public bool HasPath => !string.IsNullOrWhiteSpace(Path);
    }
    
    /// <summary>
    /// Enum representing potential locations to proxy to
    /// </summary>
    public enum ProxyTo
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
        /// Image-server/image-server cluster
        /// </summary>
        ImageServer,
        
        /// <summary>
        /// Caching reverse proxy (Varnish) 
        /// </summary>
        CachingProxy
    }
}