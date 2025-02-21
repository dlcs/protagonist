using System.Net.Http.Headers;

namespace Orchestrator.Infrastructure;

/// <summary>
/// A collection of extension methods for <see cref="HttpRequestHeaders"/>
/// </summary>
public static class HttpRequestHeadersX
{
    /// <summary>
    /// Add x-requested-by header
    /// </summary>
    public static HttpRequestHeaders WithRequestedBy(this HttpRequestHeaders headers)
    {
        headers.Add("x-requested-by", "DLCS Protagonist Yarp");
        return headers;
    }
}