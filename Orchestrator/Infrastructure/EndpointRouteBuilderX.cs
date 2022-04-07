using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Orchestrator.Infrastructure
{
    public static class EndpointRouteBuilderX
    {
        public static IEndpointRouteBuilder MapConfiguredHealthChecks(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHealthChecks("/health/imageserver", new HealthCheckOptions
            {
                AllowCachingResponses = false,
                Predicate = registration => registration.Name == "Image Server",
            });
            endpoints.MapHealthChecks("/health/detail", new HealthCheckOptions
            {
                AllowCachingResponses = false,
                ResponseWriter = WriteJsonResponse,
            });
            endpoints.MapHealthChecks("/health", new HealthCheckOptions
            {
                AllowCachingResponses = false,
                Predicate = registration => !registration.Tags.Contains("detail-only"),
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
                        new JProperty("status", pair.Value.Status.ToString())))))));

            return context.Response.WriteAsync(
                json.ToString(Formatting.Indented));
        }
    }
}