using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace DLCS.Web;

public static class EndpointBuilderX
{
    /// <summary>
    /// Add end endpoint that outputs a simple {"version": "x.y.z"} response
    /// </summary>
    public static RouteHandlerBuilder AddVersionEndpoint(this IEndpointRouteBuilder endpoints, string path = "/version",
        string fallback = "unknown") =>
        endpoints.MapGet(path, () => new
        {
            version = Environment.GetEnvironmentVariable("APP_VERSION") ?? fallback
        });
}
