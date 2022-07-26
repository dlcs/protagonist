using DLCS.Web.Requests.AssetDelivery;

namespace DLCS.Web.Response;

/// <summary>
/// Delegate that takes AssetRequest and appropriate host and returns path string
/// </summary>
public delegate string PathGenerator(IBasicPathElements assetRequest, string template);

/// <summary>
/// Generate paths related to running Dlcs instance.
/// </summary>
public interface IAssetPathGenerator
{
    /// <summary>
    /// Generate path for specified <see cref="BaseAssetRequest"/> excluding host.
    /// Uses default template replacements.
    /// </summary>
    string GetPathForRequest(IBasicPathElements assetRequest);

    /// <summary>
    /// Generate full path for specified <see cref="BaseAssetRequest"/>, including host.
    /// Uses default template replacements. 
    /// </summary>
    string GetFullPathForRequest(IBasicPathElements assetRequest);

    /// <summary>
    /// Generate full path for specified <see cref="BaseAssetRequest"/>, using provided delegate to generate
    /// path element.
    /// This can be useful for constructing paths that do not use the default path elements.
    /// </summary>
    string GetFullPathForRequest(IBasicPathElements assetRequest, PathGenerator pathGenerator);
}