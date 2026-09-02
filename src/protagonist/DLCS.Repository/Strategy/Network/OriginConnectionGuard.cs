using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Strategy.Network;

/// <summary>
/// Opens outgoing connections for origin requests, refusing any that <see cref="OriginAddressPolicy"/> blocks.
/// </summary>
/// <remarks>
/// Used as <see cref="SocketsHttpHandler.ConnectCallback"/> - this is called once per connection so every hop of
/// a redirect chain is verified, not just the origin the API consumer specified.
/// </remarks>
public class OriginConnectionGuard(OriginAddressPolicy addressPolicy, ILogger<OriginConnectionGuard> logger)
{
    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint;
        var addresses = await GetAddresses(endpoint.Host, cancellationToken);
        EnsureAllAddressesAllowed(endpoint.Host, addresses);

        // Connect to the addresses we verified, rather than the host, so that a second DNS lookup can't send us
        // somewhere else. TLS is handled above this callback so SNI + certificate validation still use the host.
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, endpoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async Task<IPAddress[]> GetAddresses(string host, CancellationToken cancellationToken)
        => IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

    /// <summary>
    /// Refuse the connection if any resolved address is blocked - a host that returns a mix of allowed and
    /// blocked addresses shouldn't be reachable by retrying until an allowed one is picked.
    /// </summary>
    private void EnsureAllAddressesAllowed(string host, IPAddress[] addresses)
    {
        foreach (var address in addresses)
        {
            var blockedBy = addressPolicy.GetBlockingRange(address);
            if (blockedBy == null) continue;

            logger.LogWarning(
                "Refusing to connect to origin host {OriginHost}: {OriginAddress} is in blocked range {BlockedRange}",
                host, address, blockedBy);
            throw new OriginAddressBlockedException(host, address, blockedBy.Value);
        }
    }
}
