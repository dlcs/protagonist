using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace DLCS.Web;

internal static class HttpContextX
{
    public static string? GetHeaderValueFromRequestOrResponse(this HttpContext? httpContext, string headerKey)
    {
        if (httpContext == null) return null;
        
        if (httpContext.Request.Headers.TryGetHeaderValue(headerKey, out var fromRequest))
        {
            return fromRequest;
        }

        if (httpContext.Response.Headers.TryGetHeaderValue(headerKey, out var fromResponse))
        {
            return fromResponse;
        }

        return null;
    }

    public static bool TryGetHeaderValue(this IHeaderDictionary headers, string headerKey,
        [NotNullWhen(true)] out string? correlationId)
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
