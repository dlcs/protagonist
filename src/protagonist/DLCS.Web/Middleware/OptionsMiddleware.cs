using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Middleware;

/// <summary>
/// Middleware for handling Options requests and setting appropriate CORS headers
/// </summary>
public class OptionsMiddleware
{
    private readonly RequestDelegate _next;

    public OptionsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext context)
    {
        if (context.Request.Method != "OPTIONS") return _next.Invoke(context);
        
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Authorization,Content-Type");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, PUT, POST, DELETE, OPTIONS");
        context.Response.StatusCode = 200;
        return context.Response.CompleteAsync();
    }
}

public static class OptionsMiddlewareX
{
    /// <summary>
    /// Add <see cref="OptionsMiddleware"/> to application builder
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseOptions(this IApplicationBuilder builder) 
        => builder.UseMiddleware<OptionsMiddleware>();
}