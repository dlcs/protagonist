using DLCS.Web.Requests.AssetDelivery;

namespace DLCS.Web.Response;

/// <summary>
/// Delegate that uses values in <see cref="IBasicPathElements"/> to make replacements in given template
/// </summary>
public delegate string PathGenerator(IBasicPathElements assetRequest, PathTemplate template);

/// <summary>
/// Generate paths related to running Dlcs instance.
/// </summary>
public interface IAssetPathGenerator
{
    /// <summary>
    /// Generate path for specified <see cref="BaseAssetRequest"/> excluding host.
    /// </summary>
    /// <param name="assetRequest"><see cref="IBasicPathElements"/> for current request</param>
    /// <param name="useNativeFormat">
    /// If true, native DLCS path /{prefix}/{version}/{customer}/{space}/{assetPath} used. Else path can differ by path.
    /// </param>
    string GetRelativePathForRequest(IBasicPathElements assetRequest, bool useNativeFormat = false);

    /// <summary>
    /// Generate full path for specified <see cref="IBasicPathElements"/>, including host.
    /// Uses default template replacements.
    /// </summary>
    /// <param name="assetRequest"><see cref="IBasicPathElements"/> for current request</param>
    /// <param name="useNativeFormat">
    /// If true, native DLCS path /{prefix}/{version}/{customer}/{space}/{assetPath} used. Else path can differ by path.
    /// </param>
    /// <param name="includeQueryParams">If true, query params are included in path. Else they are omitted</param>
    string GetFullPathForRequest(IBasicPathElements assetRequest, bool useNativeFormat = false,
        bool includeQueryParams = true);

    /// <summary>
    /// Check if the path template for current host contains {version} replacement slug
    /// </summary>
    bool PathHasVersion();
}
