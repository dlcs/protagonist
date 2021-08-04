using System.Collections.Generic;
using System.Net;
using Microsoft.Extensions.Primitives;

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
        public ProxyDestination Target { get; }
        
        /// <summary>
        /// Get path to proxy to, if rewritten
        /// </summary>
        public string? Path { get; }
        
        /// <summary>
        /// Get value indicating whether result has Path
        /// </summary>
        public bool HasPath => !string.IsNullOrWhiteSpace(Path);
        
        // TODO - differentiate between full + part path?
        public ProxyActionResult(ProxyDestination target, string? path = null)
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

        /// <summary>
        /// A collection of any Headers to set on response object. 
        /// </summary>
        public Dictionary<string, StringValues> Headers { get; } = new();

        public StatusCodeProxyResult(HttpStatusCode statusCode)
        {
            // TODO - handle message/headers? or let those be set in 
            StatusCode = statusCode;
        }

        /// <summary>
        /// Set header to return alongside statusCode
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public StatusCodeProxyResult WithHeader(string key, string value)
        {
            Headers[key] = value;
            return this;
        }
    }
    
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
        /// Image-server/image-server cluster
        /// </summary>
        ImageServer,
        
        /// <summary>
        /// Caching reverse proxy (Varnish) 
        /// </summary>
        CachingProxy,
        
        /// <summary>
        /// Proxy response from S3
        /// </summary>
        S3
    }
}