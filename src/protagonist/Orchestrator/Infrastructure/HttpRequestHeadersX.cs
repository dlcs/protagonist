using System.Net.Http.Headers;
using DLCS.Core.Strings;

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

    /// <summary>
    /// Set x-gateway-token header, used by the image-server to verify the request came from Orchestrator.
    /// Any existing value is always removed - a client supplied token is never forwarded.
    /// </summary>
    public static HttpRequestHeaders WithGatewayToken(this HttpRequestHeaders headers, string? token)
    {
        headers.Remove(GatewayTokenGenerator.TokenHeader);

        if (token.HasText())
        {
            headers.TryAddWithoutValidation(GatewayTokenGenerator.TokenHeader, token);
        }

        return headers;
    }
}
