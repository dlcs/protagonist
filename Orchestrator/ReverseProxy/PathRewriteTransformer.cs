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

        public PathRewriteTransformer(string newPath)
        {
            this.newPath = newPath;
        }
        
        public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
        {
            // Copy all request headers
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);

            // Assign the custom uri. Be careful about extra slashes when concatenating here.
            proxyRequest.RequestUri = new Uri($"{destinationPrefix}/{newPath}");
        }
    }
}