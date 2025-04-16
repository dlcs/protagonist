using System.Collections.Generic;
using System.Linq;
using System.Text;
using DLCS.Core.Collections;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Requests;

public static class HttpRequestX
{
    private const string SchemeDelimiter = "://";
    
    /// <summary>
    /// Generate a full display URL, deriving values from specified HttpRequest
    /// </summary>
    /// <param name="request">HttpRequest to generate display URL for</param>
    /// <param name="path">Path to append to URL</param>
    /// <param name="includeQueryParams">If true, query params are included in path. Else they are omitted</param>
    /// <returns>Full URL, including scheme, host, pathBase, path and queryString</returns>
    /// <remarks>
    /// based on Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(this HttpRequest request)
    /// </remarks>
    public static string GetDisplayUrl(this HttpRequest request, string? path = null, bool includeQueryParams = true)
    {
        var host = request.Host.Value ?? string.Empty;
        var scheme = request.Scheme ?? string.Empty;
        var pathBase = request.PathBase.Value ?? string.Empty;
        var queryString = includeQueryParams
            ? request.QueryString.Value ?? string.Empty
            : string.Empty;
        var pathElement = path ?? string.Empty;

        // PERF: Calculate string length to allocate correct buffer size for StringBuilder.
        var length = scheme.Length + SchemeDelimiter.Length + host.Length
                     + pathBase.Length + pathElement.Length + queryString.Length;

        return new StringBuilder(length)
            .Append(scheme)
            .Append(SchemeDelimiter)
            .Append(host)
            .Append(pathBase)
            .Append(path)
            .Append(queryString)
            .ToString();
    }

    /// <summary>
    /// Generate a display URL, deriving values from specified HttpRequest. Omitting path and query string
    /// </summary>
    public static string GetBaseUrl(this HttpRequest request)
        => request.GetDisplayUrl(path: null, includeQueryParams: false);

    /// <summary>
    /// Generate the "@id" property for a JSON-LD API response.
    /// </summary>
    public static string GetJsonLdId(this HttpRequest request) => GetDisplayUrl(request, request.Path, false);

    public static string? GetFirstQueryParamValue(this HttpRequest request, string paramName)
    {
        if (request.Query.ContainsKey(paramName))
        {
            var values = request.Query[paramName].ToArray();
            if (values.Length > 0) return values[0];
        }

        return null;
    }

    public static int? GetFirstQueryParamValueAsInt(this HttpRequest request, string paramName)
    {
        var value = GetFirstQueryParamValue(request, paramName);
        if (value.IsNullOrEmpty()) return null;
        if (int.TryParse(value, out var num))
        {
            return num;
        }

        return null;
    }
    
    public static List<string>? GetFirstQueryParamValueAsList(this HttpRequest request, string paramName)
    {
        if (request.Query.ContainsKey(paramName))
        {
            return request.Query[paramName].ToString().Split(',').ToList();
        }

        return null;
    }
}
