using System;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Web.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Middleware;

/// <summary>
/// Middleware that establishes a correlation-id for the current request, sourced from HttpHeader. If the header is not
/// present then one is generated. The id is set on both the request and the response and added to the Serilog
/// LogContext for the duration of the request.
/// </summary>
/// <remarks>
/// Register this as early in the pipeline as possible - it sets a response header, which is only possible before the
/// response has started, and any log event raised before it runs will not be correlated.
/// </remarks>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        // Setting these here, rather than lazily when a log event is raised, means both are populated for the whole
        // request and neither depends on the response not having started yet.
        context.Request.Headers[CorrelationIdContext.HeaderKey] = correlationId;
        context.Response.Headers[CorrelationIdContext.HeaderKey] = correlationId;

        using (CorrelationIdContext.Set(correlationId))
        {
            await next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdContext.HeaderKey, out var values))
        {
            var fromRequest = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(fromRequest)) return fromRequest;
        }

        return Guid.NewGuid().ToString();
    }
}

public static class CorrelationIdMiddlewareX
{
    /// <summary>
    /// Add <see cref="CorrelationIdMiddleware"/> to application builder. Register as early as possible in the pipeline.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        => builder.UseMiddleware<CorrelationIdMiddleware>();
}
