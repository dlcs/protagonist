using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Repository.Strategy;

/// <summary>
/// Helper methods for constructing OriginResponse obejcts
/// </summary>
internal static class OriginResponseHelpers
{
    /// <summary>
    /// Create <see cref="OriginResponse"/> from provided <see cref="HttpResponseMessage"/>
    /// </summary>
    public static async Task<OriginResponse> CreateOriginResponse(this HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var content = response.Content;
        return new OriginResponse(await content.ReadAsStreamAsync(cancellationToken))
            .WithContentLength(content.Headers.ContentLength)
            .WithContentType(content.Headers.ContentType?.MediaType);
    }
}
