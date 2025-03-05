using System.Collections.Generic;
using System.Net;
using Microsoft.Extensions.Primitives;
using Orchestrator.Assets;

namespace Orchestrator.Infrastructure.ReverseProxy;

/// <summary>
/// Marker interface for result of proxy processing logic.
/// </summary>
public interface IProxyActionResult
{
    /// <summary>
    /// A collection of any Headers to set on response object. 
    /// </summary>
    Dictionary<string, StringValues> Headers { get; }
} 

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
        bool requiresAuth,
        string? path = null) : base(ProxyDestination.ImageServer, requiresAuth, path)
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
    
    /// <summary>
    /// Whether this request requires authentication to view
    /// </summary>
    public bool RequiresAuth { get; }

    /// <summary>
    /// A collection of any Headers to set on response object. 
    /// </summary>
    public Dictionary<string, StringValues> Headers { get; } = new();
    
    // TODO - differentiate between full + part path?
    public ProxyActionResult(ProxyDestination target, bool requiresAuth, string? path = null)
    {
        Target = target;
        RequiresAuth = requiresAuth;
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
    
    public static StatusCodeResult NotFound => new(HttpStatusCode.NotFound);
}

public static class ProxyActionResultsX
{
    /// <summary>
    /// Set header to return alongside statusCode
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static IProxyActionResult WithHeader(this IProxyActionResult result, string key, string value)
    {
        result.Headers[key] = value;
        return result;
    }
}
