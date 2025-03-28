using System;
using System.Net.Http;
using System.Threading;
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
        string destinationPrefix, CancellationToken cancelationToken)
    {
        // Copy all request headers
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancelationToken);

        // Assign the custom uri. Be careful about extra slashes when concatenating here.
        proxyRequest.RequestUri = rewriteWholePath 
            ? new Uri(proxyAction.Path) 
            : GetNewDestination(destinationPrefix);
        
        // TODO - handle x-forwarded-* headers?
        proxyRequest.Headers.Host = proxyRequest.RequestUri.Authority;
        proxyRequest.Headers.WithRequestedBy();

        // NOTE(DG) - this is a hardcoded list for now but we may want to make this config driven later
        if (proxyAction.Target == ProxyDestination.S3)
        {
            // Added by CloudFront, can cause issues proxying to S3
            proxyRequest.Headers.Remove("x-amz-cf-id");
        }
    }

    public override ValueTask<bool> TransformResponseAsync(
        HttpContext httpContext,
        HttpResponseMessage? proxyResponse)
    {
        base.TransformResponseAsync(httpContext, proxyResponse);

        CleanResponseHeaders(httpContext);
        EnsureCorsHeaders(httpContext);

        var isDownstreamError = IsDownstreamError(proxyResponse);
        EnsureCacheHeaders(httpContext, isDownstreamError);
        SetCustomHeaders(httpContext, isDownstreamError);

        return new ValueTask<bool>(true);
    }

    private bool IsDownstreamError(HttpResponseMessage? proxyResponse) 
        => proxyResponse == null || !proxyResponse.IsSuccessStatusCode;

    private void EnsureCacheHeaders(HttpContext httpContext, bool isDownstreamError)
    {
        const string cacheControlHeader = "Cache-Control";

        if (isDownstreamError)
        {
            httpContext.Response.Headers[cacheControlHeader] = "max-age=60";
            return;
        }
        
        if (proxyAction.Target is not ProxyDestination.ImageServer and not ProxyDestination.SpecialServer) return;

        var cacheControl = proxyAction.RequiresAuth
            ? "private, max-age=600"
            : "public, s-maxage=2419200, max-age=2419200, stale-if-error=86400";
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
    
    private void SetCustomHeaders(HttpContext httpContext, bool isDownstreamError)
    {
        if (isDownstreamError) return;
        
        foreach (var (key, value) in proxyAction.Headers)
        {
            httpContext.Response.Headers[key] = value;
        }
    }
    
    private void CleanResponseHeaders(HttpContext httpContext)
    {
        if (proxyAction.Target is ProxyDestination.ImageServer or ProxyDestination.SpecialServer)
        {
            httpContext.Response.Headers.Remove("link");
            httpContext.Response.Headers.Remove("server");
        }

        if (proxyAction.Target == ProxyDestination.S3)
        {
            httpContext.Response.Headers.Remove("x-amz-tagging-count");
            httpContext.Response.Headers.Remove("x-amz-storage-class");
        }
    }

    private Uri GetNewDestination(string destinationPrefix)
        => new(destinationPrefix.ToConcatenated('/', proxyAction.Path));
}
