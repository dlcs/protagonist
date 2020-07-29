using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Middleware
{
    /// <summary>
    /// Middleware that adds "Access-Control-Allow-Origin:*" header.
    /// </summary>
    public class AlwaysCorsMiddleware
    {
        private readonly RequestDelegate next;

        public AlwaysCorsMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            await next(httpContext);
        }
    }
}