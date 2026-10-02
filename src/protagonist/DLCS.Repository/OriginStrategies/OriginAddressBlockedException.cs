using System;
using System.Net;

namespace DLCS.Repository.OriginStrategies;

/// <summary>
/// Exception raised when an origin resolves to an IP address that we are not permitted to connect to.
/// </summary>
public class OriginAddressBlockedException(string host, IPAddress address, IPNetwork blockedBy)
    : Exception($"Origin host '{host}' resolves to an IP address that is not permitted")
{
    /// <summary>Host, as specified in the origin, that was refused.</summary>
    public string Host { get; } = host;

    /// <summary>Address that <see cref="Host"/> resolved to.</summary>
    public IPAddress Address { get; } = address;

    /// <summary>Range that <see cref="Address"/> falls within.</summary>
    public IPNetwork BlockedBy { get; } = blockedBy;

    /// <summary>
    /// Find an <see cref="OriginAddressBlockedException"/> in the chain of inner exceptions, if present.
    /// HttpClient wraps anything raised from SocketsHttpHandler.ConnectCallback in an HttpRequestException.
    /// </summary>
    /// <returns>Located exception, or null if the chain contains none</returns>
    public static OriginAddressBlockedException? FindInChain(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is OriginAddressBlockedException blocked) return blocked;
        }

        return null;
    }
}
