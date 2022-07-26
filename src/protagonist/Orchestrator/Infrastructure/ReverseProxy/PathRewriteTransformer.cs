using System;
using System.Net.Http;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Infrastructure.ReverseProxy;

/// <summary>
/// <see cref="HttpTransformer"/> that redirects to new path.
/// </summary>
public class PathRewriteTransformer : HttpTransformer
{
    private readonly ProxyActionResult proxyAction;
    private readonly bool rewriteWholePath;

    public PathRewriteTransformer(ProxyActionResult proxyAction, bool rewriteWholePath = false)
    {
        this.proxyAction = proxyAction;
        this.rewriteWholePath = rewriteWholePath;
    }

    public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest,
        string destinationPrefix)
    {
        // Copy all request headers
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);

        // Assign the custom uri. Be careful about extra slashes when concatenating here.
        proxyRequest.RequestUri = rewriteWholePath 
            ? new Uri(proxyAction.Path) 
            : GetNewDestination(destinationPrefix);
        
        // TODO - handle x-forwarded-* headers?
        proxyRequest.Headers.Host = proxyRequest.RequestUri.Authority;
        proxyRequest.Headers.WithRequestedBy();
    }

    public override ValueTask<bool> TransformResponseAsync(
        HttpContext httpContext,
        HttpResponseMessage? proxyResponse)
    {
        base.TransformResponseAsync(httpContext, proxyResponse);
        
        EnsureCorsHeaders(httpContext);
        EnsureCacheHeaders(httpContext);
        SetCustomHeaders(httpContext);

        return new ValueTask<bool>(true);
    }

    private void EnsureCacheHeaders(HttpContext httpContext)
    {
        const string cacheControlHeader = "Cache-Control";
        if (proxyAction.Target != ProxyDestination.ImageServer) return;
        var cacheControl = proxyAction.RequiresAuth
            ? "private, max-age=600"
            : "public, s-maxage=2419200, max-age=2419200";
        httpContext.Response.Headers[cacheControlHeader] = cacheControl;
    }

    private static void EnsureCorsHeaders(HttpContext httpContext)
    {
        const string accessControlAllowOrigin = "Access-Control-Allow-Origin";
        if (!httpContext.Response.Headers.ContainsKey(accessControlAllowOrigin))
        {
            httpContext.Response.Headers.Add(accessControlAllowOrigin, "*");
        }
    }
    
    private void SetCustomHeaders(HttpContext httpContext)
    {
        foreach (var (key, value) in proxyAction.Headers)
        {
            httpContext.Response.Headers[key] = value;
        }
    }

    private Uri GetNewDestination(string destinationPrefix)
        => new(destinationPrefix.ToConcatenated('/', proxyAction.Path));
}