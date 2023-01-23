using System;
using Microsoft.AspNetCore.Http;
using Serilog.Events;

namespace DLCS.Web.Logging;

public static class LogHelper
{
    /// <summary>
    /// Set log level to Verbose for health-check requests
    /// </summary>
    /// <remarks>
    /// See https://andrewlock.net/using-serilog-aspnetcore-in-asp-net-core-3-excluding-health-check-endpoints-from-serilog-request-logging/#using-a-custom-log-level-for-health-check-endpoint-requests
    /// </remarks>
    public static LogEventLevel ExcludeHealthChecks(HttpContext ctx, double _, Exception? ex) 
        => ex != null
            ? LogEventLevel.Error 
            : ctx.Response.StatusCode > 499 
                ? LogEventLevel.Error 
                : IsHealthCheckEndpoint(ctx) // Not an error, check if it was a health check
                    ? LogEventLevel.Verbose // Was a health check, use Verbose
                    : LogEventLevel.Information;
    
    private static bool IsHealthCheckEndpoint(HttpContext ctx)
    {
        var endpoint = ctx.GetEndpoint();
        if (endpoint != null) // same as !(endpoint is null)
        {
            return string.Equals(
                endpoint.DisplayName, 
                "Health checks",
                StringComparison.Ordinal);
        }
        // No endpoint, so not a health check endpoint
        return false;
    }
}
