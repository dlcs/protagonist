using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Engine.Infrastructure;

public static class EndpointRouteBuilderX
{
    public static IEndpointRouteBuilder MapConfiguredHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/detail", new HealthCheckOptions
        {
            AllowCachingResponses = false,
            ResponseWriter = WriteJsonResponse,
        });
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            AllowCachingResponses = false,
        });
        return endpoints;
    }
        
    private static Task WriteJsonResponse(HttpContext context, HealthReport result)
    {
        context.Response.ContentType = "application/json";

        var json = new JObject(
            new JProperty("status", result.Status.ToString()),
            new JProperty("results", new JObject(result.Entries.Select(pair =>
                new JProperty(pair.Key, new JObject(
                    new JProperty("status", pair.Value.Status.ToString()),
                    new JProperty("data",
                        new JObject(pair.Value.Data.Select(d => new JProperty(d.Key, d.Value))))))))));

        return context.Response.WriteAsync(json.ToString(Formatting.Indented));
    }
}