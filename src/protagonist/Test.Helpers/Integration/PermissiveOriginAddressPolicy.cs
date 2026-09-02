using System.Net;
using DLCS.Repository.OriginStrategies;

namespace Test.Helpers.Integration;

/// <summary>
/// <see cref="IOriginAddressPolicy"/> that permits every address, for tests that serve an origin from a local stub.
/// </summary>
public class PermissiveOriginAddressPolicy : IOriginAddressPolicy
{
    public IPNetwork? GetBlockingRange(IPAddress address) => null;
}
