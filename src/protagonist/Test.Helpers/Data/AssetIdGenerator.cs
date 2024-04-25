using System.Runtime.CompilerServices;
using DLCS.Core.Types;

namespace Test.Helpers.Data;

public static class AssetIdGenerator
{
    /// <summary>
    /// Generate new <see cref="AssetId"/> using calling function as "asset" part by default
    /// </summary>
    public static AssetId GetAssetId(int customer = 99, int space = 1, [CallerMemberName] string asset = "") 
        => new(customer, space, asset);
}