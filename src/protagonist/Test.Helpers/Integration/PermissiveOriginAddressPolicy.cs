using System.Net;
using DLCS.Repository.Strategy.Network;

namespace Test.Helpers.Integration;

/// <summary>
/// <see cref="OriginAddressPolicy"/> that permits every address, for tests that serve an origin from a local stub.
/// </summary>
public class PermissiveOriginAddressPolicy : OriginAddressPolicy
{
    public override IPNetwork? GetBlockingRange(IPAddress address) => null;
}
