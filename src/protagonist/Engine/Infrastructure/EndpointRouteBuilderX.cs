using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Engine.Infrastructure;

public static class EndpointRouteBuilderX
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Configure healthcheck endpoints
    /// </summary>
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
        
        /* Outputs object in format:
         {
            "status": "Healthy|Unhealthy",
            "results": {
                "DLCS-DB": {
                    "status": "Healthy|Unhealthy",
                    "data": {}
                },
                "Registered Queues": {
                    "status": "Healthy|Degraded|Unhealthy",
                    "data": {
                        "queue-1": "Listening|Not started|Stopped",
                        "queue-2": "Listening|Not started|Stopped",
                    }
                }
            }
        } */

        var jsonObject = new JsonObject
        {
            ["status"] = result.Status.ToString(),
            ["results"] = new JsonObject(result.Entries.Select(kvp => KeyValuePair.Create<string, JsonNode>(kvp.Key,
                new JsonObject
                {
                    ["status"] = kvp.Value.Status.ToString(),
                    ["data"] = new JsonObject(kvp.Value.Data.Select(d =>
                        KeyValuePair.Create<string, JsonNode>(d.Key, d.Value.ToString())))
                })))
        };

        return context.Response.WriteAsync(jsonObject.ToJsonString(JsonSerializerOptions));
    }
}