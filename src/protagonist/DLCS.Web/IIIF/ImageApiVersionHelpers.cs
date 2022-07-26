using System;
using System.Linq;
using DLCS.Core.Collections;
using IIIF.ImageApi;
using Microsoft.AspNetCore.Http;
using Version = IIIF.ImageApi.Version;

namespace DLCS.Web.IIIF;

/// <summary>
/// Class containing helpers for dealing with parsing values for IIIF Image Api
/// </summary>
public static class ImageApiVersionHelpers
{
    /// <summary>
    /// Parse Accepts headers to find requested IIIF ImageApi version, falling back to specified version if not found.  
    /// </summary>
    /// <param name="request">Current HttpRequest</param>
    /// <param name="fallbackVersion">ImageApi version to fallback to if no type found.</param>
    /// <returns>ImageApi version</returns>
    public static Version GetIIIFImageApiVersion(this HttpRequest request, Version fallbackVersion)
    {
        var requestedVersion = request.GetTypedHeaders().Accept.GetIIIFImageApiType();
        var version = requestedVersion == Version.Unknown ? fallbackVersion : requestedVersion;
        return version;
    }

    /// <summary>
    /// Parse "version" RouteValues and convert to IIIF ImageApi version.
    /// </summary>
    /// <param name="request">Current HttpRequest</param>
    /// <returns>ImageApi version</returns>
    public static Version? GetIIIFImageApiVersionFromRoute(this HttpRequest request)
    {
        if (!request.RouteValues.TryGetValue("version", out var versionValue) || versionValue == null)
        {
            return null;
        }

        return versionValue.ToString().ParseToIIIFImageApiVersion();
    }
    
    /// <summary>
    /// Get IIIF Image version from "version" RouteValue. Will be in form "v1", "v2" etc
    /// </summary>
    /// <param name="version">Current HttpRequest</param>
    /// <returns>ImageApi version</returns>
    public static Version? ParseToIIIFImageApiVersion(this string? version)
    {
        if (!version.IsNullOrEmpty())
        {
            switch (version![1])
            {
                case '2':
                    return Version.V2;
                case '3':
                    return Version.V3;
            }
        }

        return null;
    }

    /// <summary>
    /// Get Content-Type to set for ImageApi request
    /// </summary>
    /// <param name="request">Current HttpRequest</param>
    /// <param name="imageApiVersion"></param>
    /// <returns></returns>
    /// <remarks>
    /// For V2, only return application/ld+json if client specified via Accept header see,
    /// https://iiif.io/api/image/2.1/#image-information-request
    /// </remarks>
    public static string GetIIIFContentType(this HttpRequest request, Version imageApiVersion)
        => imageApiVersion switch
        {
            Version.V3 => ContentTypes.V3,
            Version.V2 => request.GetTypedHeaders().Accept.Any(h => h.MatchesMediaType("application/ld+json"))
                ? "application/ld+json"
                : "application/json",
            _ => throw new ArgumentOutOfRangeException(nameof(imageApiVersion), imageApiVersion,
                "Unable to determine ContentType for ImageApi version")
        };
}