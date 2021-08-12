using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.ReverseProxy
{
    /// <summary>
    /// <see cref="HttpTransformer"/> that redirects to new path.
    /// </summary>
    public class PathRewriteTransformer : HttpTransformer
    {
        private readonly string newPath;
        private readonly bool rewriteWholePath;

        public PathRewriteTransformer(string newPath, bool rewriteWholePath = false)
        {
            this.newPath = newPath;
            this.rewriteWholePath = rewriteWholePath;
        }

        public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest,
            string destinationPrefix)
        {
            // Copy all request headers
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);

            // TODO - handle x-forwarded-* headers?
            proxyRequest.Headers.Host = new Uri(destinationPrefix).Authority;

            // Assign the custom uri. Be careful about extra slashes when concatenating here.
            proxyRequest.RequestUri = rewriteWholePath ? new Uri(newPath) : GetNewDestination(destinationPrefix);
        }

        private Uri GetNewDestination(string destinationPrefix)
            => new(destinationPrefix[^1] == '/'
                ? $"{destinationPrefix}{newPath}"
                : $"{destinationPrefix}/{newPath}");
    }
}