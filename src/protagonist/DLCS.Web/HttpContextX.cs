using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace DLCS.Web;

internal static class HttpContextX
{
    public static string? GetHeaderValueFromRequestOrResponse(this HttpContext? httpContext, string headerKey)
    {
        if (httpContext == null) return null;
        
        if (TryGetCorrelationId(httpContext.Request.Headers, headerKey, out var fromRequest))
        {
            return fromRequest;
        }

        if (TryGetCorrelationId(httpContext.Response.Headers, headerKey, out var fromResponse))
        {
            return fromResponse;
        }

        return null;
    }
    
    private static bool TryGetCorrelationId(IHeaderDictionary headers, string headerKey, out string? correlationId)
    {
        correlationId = null;
        if (headers.TryGetValue(headerKey, out var values))
        {
            correlationId = values.FirstOrDefault();
            return !StringValues.IsNullOrEmpty(correlationId);
        }

        return false;
    }
}