using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using DLCS.AWS.S3.Models;
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
    [MemberNotNullWhen(true, nameof(Path))]
    public bool HasPath => !string.IsNullOrWhiteSpace(Path);
    
    /// <summary>
    /// Whether this request requires authentication to view
    /// </summary>
    public bool RequiresAuth { get; }

    /// <summary>
    /// Optional signature, sent to downstream service in <c>x-gateway-token</c> request header, proving that the
    /// request originated from Orchestrator. See <see cref="Orchestrator.Infrastructure.GatewayTokenGenerator"/>
    /// </summary>
    public string? GatewayToken { get; init; }

    /// <summary>
    /// A collection of any Headers to set on response object. 
    /// </summary>
    public Dictionary<string, StringValues> Headers { get; } = new();
    
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
public class StatusCodeResult(HttpStatusCode statusCode, string? message = null) : IProxyActionResult
{
    /// <summary>
    /// StatusCode to return
    /// </summary>
    public HttpStatusCode StatusCode { get; } = statusCode;
    
    /// <summary>
    /// Optional message to return with status code
    /// </summary>
    public string? Message { get; } = message;

    /// <summary>
    /// A collection of any Headers to set on response object. 
    /// </summary>
    public Dictionary<string, StringValues> Headers { get; } = new();

    public static StatusCodeResult NotFound => new(HttpStatusCode.NotFound);
}

/// <summary>
/// Result for json objects that require the top-level <c>id</c> to be rewritten on the fly.
/// </summary>
public class IdRewriteProxyActionResult(ObjectInBucket source, string newId) : IProxyActionResult
{
    /// <summary>
    /// The S3 location of the object to rewrite.
    /// </summary>
    public ObjectInBucket Source { get; } = source;

    /// <summary>
    /// The new value to write into the top-level <c>id</c> property.
    /// </summary>
    public string NewId { get; } = newId;

    /// <summary>
    /// Maximum permitted size of the object in bytes. Requests exceeding this limit will be rejected.
    /// </summary>
    public long? MaxSizeBytes { get; init; }

    /// <summary>
    /// A collection of any Headers to set on response object.
    /// </summary>
    public Dictionary<string, StringValues> Headers { get; } = new();
}

public static class ProxyActionResultsX
{
    /// <summary>
    /// Add headers to <see cref="IProxyActionResult"/> object.
    /// </summary>
    public static IProxyActionResult WithHeader(this IProxyActionResult result, string key, string value)
    {
        result.Headers[key] = value;
        return result;
    }
}
