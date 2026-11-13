using System;
using System.Collections.Generic;
using System.Linq;

namespace DLCS.Model.Auth;

/// <summary>
/// Extension methods for role URIs
/// </summary>
public static class RoleX
{
    /// <summary>
    /// Prefix of final path segment of role URI that indicates role is granted by client IP address
    /// </summary>
    public const string IpRolePrefix = "ip:";

    /// <summary>
    /// Prefix of final path segment of role URI that indicates role is granted by an appointment
    /// </summary>
    public const string AppointmentRolePrefix = "appointment:";

    /// <summary>
    /// Check if role URI is an IP role, i.e. the final path segment starts with "ip:"
    /// (e.g. https://api.dlcs.io/customers/2/roles/ip:reading-room)
    /// </summary>
    /// <remarks>
    /// IP roles are granted by IIIF Auth2 service based on client IP, rather than a session, so require the client IP
    /// to be forwarded and can be granted without a cookie or access token
    /// </remarks>
    public static bool IsIpRole(this string? roleUri) => HasRolePrefix(roleUri, IpRolePrefix);

    /// <summary>
    /// Check if any of the provided role URIs are IP roles
    /// </summary>
    public static bool ContainsIpRole(this IEnumerable<string>? roles)
        => roles?.Any(r => r.IsIpRole()) ?? false;

    /// <summary>
    /// Check if role URI is an appointment role, i.e. the final path segment starts with "appointment:"
    /// (e.g. https://api.dlcs.io/customers/2/roles/appointment:token-based)
    /// </summary>
    /// <remarks>
    /// Appointment roles are granted by IIIF Auth2 service based on appointments held by the session and the asset's
    /// metadata, so require asset metadata to be sent
    /// </remarks>
    public static bool IsAppointmentRole(this string? roleUri) => HasRolePrefix(roleUri, AppointmentRolePrefix);

    /// <summary>
    /// Check if any of the provided role URIs are appointment roles
    /// </summary>
    public static bool ContainsAppointmentRole(this IEnumerable<string>? roles)
        => roles?.Any(r => r.IsAppointmentRole()) ?? false;

    private static bool HasRolePrefix(string? roleUri, string prefix)
    {
        if (string.IsNullOrWhiteSpace(roleUri)) return false;

        var trimmed = roleUri.AsSpan().TrimEnd('/');
        var lastSegment = trimmed[(trimmed.LastIndexOf('/') + 1)..];
        return lastSegment.StartsWith(prefix, StringComparison.Ordinal);
    }
}
