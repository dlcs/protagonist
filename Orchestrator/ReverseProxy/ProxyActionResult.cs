using System.Net;

namespace Orchestrator.ReverseProxy
{
    /// <summary>
    /// Marker interface for result of proxy processing logic.
    /// </summary>
    public interface IProxyActionResult {}

    /// <summary>
    /// Result for actions that should be proxied to downstream service.
    /// </summary>
    public class ProxyActionResult : IProxyActionResult
    {
        /// <summary>
        /// Get downstream system to Proxy to
        /// </summary>
        public ProxyTo Target { get; }
        
        /// <summary>
        /// Get path to proxy to, if rewritten
        /// </summary>
        public string? Path { get; }
        
        /// <summary>
        /// Get value indicating whether result has Path
        /// </summary>
        public bool HasPath => !string.IsNullOrWhiteSpace(Path);
        
        public ProxyActionResult(ProxyTo target, string? path = null)
        {
            Target = target;
            Path = !string.IsNullOrWhiteSpace(path) && path[0] == '/' ? path[1..] : path;
        }
    }

    /// <summary>
    /// Result for proxy actions that should be shortcut to return status code.
    /// </summary>
    public class StatusCodeProxyResult : IProxyActionResult
    {
        /// <summary>
        /// StatusCode to return
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        public StatusCodeProxyResult(HttpStatusCode statusCode)
        {
            // TODO - handle message/headers? or let those be set in 
            StatusCode = statusCode;
        }
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