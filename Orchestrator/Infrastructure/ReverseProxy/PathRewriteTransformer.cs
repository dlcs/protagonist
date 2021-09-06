using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Infrastructure.ReverseProxy
{
    /// <summary>
    /// <see cref="HttpTransformer"/> that redirects to new path.
    /// </summary>
    public class PathRewriteTransformer : HttpTransformer
    {
        private readonly string newPath;
        private readonly ProxyDestination proxyDestination;
        private readonly bool rewriteWholePath;
        private readonly bool isRestrictedContent;

        public PathRewriteTransformer(string newPath,
            ProxyDestination proxyDestination,
            bool rewriteWholePath = false,
            bool isRestrictedContent = false)
        {
            this.newPath = newPath;
            this.proxyDestination = proxyDestination;
            this.rewriteWholePath = rewriteWholePath;
            this.isRestrictedContent = isRestrictedContent;
        }

        public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest,
            string destinationPrefix)
        {
            // Copy all request headers
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);

            // Assign the custom uri. Be careful about extra slashes when concatenating here.
            proxyRequest.RequestUri = rewriteWholePath ? new Uri(newPath) : GetNewDestination(destinationPrefix);
            
            // TODO - handle x-forwarded-* headers?
            proxyRequest.Headers.Host = proxyRequest.RequestUri.Authority;
            proxyRequest.Headers.WithRequestedBy();
        }

        public override ValueTask<bool> TransformResponseAsync(
            HttpContext httpContext,
            HttpResponseMessage proxyResponse)
        {
            base.TransformResponseAsync(httpContext, proxyResponse);
            
            EnsureCorsHeaders(httpContext);
            EnsureCacheHeaders(httpContext);

            return new ValueTask<bool>(true);
        }

        private void EnsureCacheHeaders(HttpContext httpContext)
        {
            // TODO - read CustomHeaders data and set accordingly
            const string cacheControlHeader = "Cache-Control";
            if (proxyDestination == ProxyDestination.ImageServer)
            {
                var cacheControl = isRestrictedContent
                    ? "private, max-age=600"
                    : "public, s-maxage=2419200, max-age=2419200";
                httpContext.Response.Headers[cacheControlHeader] = cacheControl;
            }
        }

        private static void EnsureCorsHeaders(HttpContext httpContext)
        {
            const string accessControlAllowOrigin = "Access-Control-Allow-Origin";
            if (!httpContext.Response.Headers.ContainsKey(accessControlAllowOrigin))
            {
                httpContext.Response.Headers.Add(accessControlAllowOrigin, "*");
            }
        }

        private Uri GetNewDestination(string destinationPrefix)
            => new(destinationPrefix[^1] == '/'
                ? $"{destinationPrefix}{newPath}"
                : $"{destinationPrefix}/{newPath}");
    }
}