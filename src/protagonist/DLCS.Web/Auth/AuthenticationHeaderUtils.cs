using System;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Auth;

/// <summary>
/// Utilities for dealing with http requests and authentication
/// </summary>
public static class AuthenticationHeaderUtils
{
    /// <summary>
    /// Scheme for Basic authentication
    /// </summary>
    public const string BasicScheme = "Basic";
    
    /// <summary>
    /// Scheme for Bearer authentication
    /// </summary>
    public const string BearerTokenScheme = "Bearer";

    /// <summary>
    /// Attempt to parse <see cref="AuthenticationHeaderValue"/> from provided request. Optionally filtering by
    /// scheme, if provided.
    /// </summary>
    /// <param name="request"><see cref="HttpRequest"/> object to check</param>
    /// <param name="scheme">Optional auth scheme to filter check against. If not provided any scheme is valid.</param>
    /// <returns>Parsed <see cref="AuthenticationHeaderValue"/> if found and matches optional scheme.</returns>
    public static AuthenticationHeaderValue? GetAuthHeaderValue(this HttpRequest request, string? scheme = null)
    {
        if (!AuthenticationHeaderValue.TryParse(request.Headers.Authorization,
                out AuthenticationHeaderValue? headerValue))
        {
            // Not found or invalid Authorization header
            return null;
        }

        if (string.IsNullOrWhiteSpace(scheme))
        {
            return headerValue;
        }

        return !scheme.Equals(headerValue.Scheme, StringComparison.OrdinalIgnoreCase) ? null : headerValue;
    }
}