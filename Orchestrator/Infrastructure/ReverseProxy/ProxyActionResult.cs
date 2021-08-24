using System.Collections.Generic;
using System.Net;
using Microsoft.Extensions.Primitives;
using Orchestrator.Assets;

namespace Orchestrator.Infrastructure.ReverseProxy
{
    /// <summary>
    /// Marker interface for result of proxy processing logic.
    /// </summary>
    public interface IProxyActionResult {} // TODO -rename this?

    /// <summary>
    /// Results for actions that is for image orchestration
    /// </summary>
    public class ProxyImageServerResult : ProxyActionResult
    {
        /// <summary>
        /// <see cref="OrchestrationImage"/> for current request
        /// </summary>
        public OrchestrationImage OrchestrationImage { get; }
        
        public ProxyImageServerResult(
            OrchestrationImage orchestrationImage,
            ProxyDestination target,
            string? path = null) : base(target, path)
        {
            OrchestrationImage = orchestrationImage;
        }
    }

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
    public class StatusCodeResult : IProxyActionResult
    {
        /// <summary>
        /// StatusCode to return
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// A collection of any Headers to set on response object. 
        /// </summary>
        public Dictionary<string, StringValues> Headers { get; } = new();

        public StatusCodeResult(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// Set header to return alongside statusCode
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public StatusCodeResult WithHeader(string key, string value)
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
        /// Proxy response from S3
        /// </summary>
        S3
    }
}