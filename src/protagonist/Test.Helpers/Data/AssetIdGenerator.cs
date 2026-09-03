using System.Runtime.CompilerServices;
using DLCS.Core.Types;

namespace Test.Helpers.Data;

public static class AssetIdGenerator
{
    /// <summary>
    /// Generate new <see cref="AssetId"/> using calling function as "asset" part by default
    /// </summary>
    public static AssetId GetAssetId(int customer = 99, int space = 1, [CallerMemberName] string asset = "",
        string assetPostfix = "")
        => new(customer, space, $"{asset}{assetPostfix}");
}

public static class AdjunctIdGenerator
{
    /// <summary>
    /// Generate new test adjunct id using calling function and optional postfix
    /// </summary>
    public static string GetAdjunctId([CallerMemberName] string adjunct = "", string postfix = "")
        => $"{adjunct}{postfix}";
}
