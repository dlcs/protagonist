using System;
using System.Diagnostics.CodeAnalysis;
using DLCS.Core.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Orchestrator.Features.Images;

/// <summary>
/// Helpers for generating a Content-Disposition header from the optional <c>?filename=</c> query parameter.
/// See RFC 026-download-filename
/// </summary>
public static class DownloadFilename
{
    /// <summary>
    /// Name of query parameter containing filename template
    /// </summary>
    public const string QueryParameter = "filename";

    /// <summary>
    /// Template placeholder that resolves to full asset id, in format {customer}_{space}_{image}
    /// </summary>
    public const string IdPlaceholder = "{id}";

    /// <summary>
    /// Attempt to generate a Content-Disposition header value from <c>?filename=</c> query parameter.
    /// Only a value of exactly <c>{id}</c> (case-insensitive) is currently supported, any other value is ignored.
    /// </summary>
    /// <param name="query">Incoming request query collection</param>
    /// <param name="assetId">Id of requested asset</param>
    /// <param name="format">Requested IIIF image format (e.g. "jpg"), used as file extension</param>
    /// <param name="headerValue">Content-Disposition header value, if supported template provided</param>
    /// <returns>true if supported template provided and header value generated, else false</returns>
    public static bool TryGetContentDisposition(IQueryCollection query, AssetId assetId, string format,
        [NotNullWhen(true)] out string? headerValue)
    {
        headerValue = null;
        if (!query.TryGetValue(QueryParameter, out var template) || template.Count != 1) return false;
        if (!IdPlaceholder.Equals(template[0], StringComparison.OrdinalIgnoreCase)) return false;

        var filename = $"{assetId.Customer}_{assetId.Space}_{assetId.Asset.Replace('/', '_')}.{format}";
        var contentDisposition = new ContentDispositionHeaderValue("attachment");
        contentDisposition.SetHttpFileName(filename);
        headerValue = contentDisposition.ToString();
        return true;
    }
}
