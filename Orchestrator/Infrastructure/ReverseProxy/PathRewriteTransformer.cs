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
        private readonly bool rewriteWholePath;

        public PathRewriteTransformer(
            string newPath,
            bool rewriteWholePath = false)
        {
            this.newPath = newPath;
            this.rewriteWholePath = rewriteWholePath;
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
        }

        public override ValueTask<bool> TransformResponseAsync(
            HttpContext httpContext,
            HttpResponseMessage proxyResponse)
        {
            // TODO - this will need to be aware of public/not-public assets and set headers accordingly
            base.TransformResponseAsync(httpContext, proxyResponse);
            
            httpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");

            return new ValueTask<bool>(true);
        }

        private Uri GetNewDestination(string destinationPrefix)
            => new(destinationPrefix[^1] == '/'
                ? $"{destinationPrefix}{newPath}"
                : $"{destinationPrefix}/{newPath}");
    }
}