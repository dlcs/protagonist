using DLCS.Web.Requests.AssetDelivery;

namespace DLCS.Web.Response;

/// <summary>
/// Delegate that uses values in <see cref="IBasicPathElements"/> to make replacements in given template
/// </summary>
public delegate string PathGenerator(IBasicPathElements assetRequest, string template);

/// <summary>
/// Generate paths related to running Dlcs instance.
/// </summary>
public interface IAssetPathGenerator
{
    /// Generate full path for specified <see cref="IBasicPathElements"/>, including host.
    /// Uses default template replacements.
    /// <param name="assetRequest"><see cref="IBasicPathElements"/></param>
    /// <param name="useNativeFormat"></param>
    /// <returns></returns>
    string GetFullPathForRequest(IBasicPathElements assetRequest, bool useNativeFormat = false);
    
    /// <summary>
    /// Generate full path for specified <see cref="IBasicPathElements"/>, using provided delegate to generate
    /// path element.
    /// This can be useful for constructing paths that do not use the default path elements.
    /// </summary>
    string GetFullPathForRequest(IBasicPathElements assetRequest, PathGenerator pathGenerator,
        bool useNativeFormat = false);
}