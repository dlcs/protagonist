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
    /// <summary>
    /// Generate path for specified <see cref="BaseAssetRequest"/> excluding host.
    /// Uses default template replacements.
    /// </summary>
    /// <param name="assetRequest"></param>
    /// <param name="useNativeFormat"></param>
    /// <returns></returns>
    string GetRelativePathForRequest(IBasicPathElements assetRequest, bool useNativeFormat = false);
    
    /// <summary>
    /// Generate full path for specified <see cref="IBasicPathElements"/>, including host.
    /// Uses default template replacements.
    /// </summary>
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

// DONE
// GetFullyQualifiedId - versioned, always standard path, IIIFCanvasFactory.GetFullyQualifiedId ln 246
// GetFullQualifiedImagePath - not versioned, always standard path, IIIFCanvasFactory.GetFullQualifiedImagePath ln 233
// GetFullyQualifiedId - versioned, always standard path, GetManifestForAsset.GetFullyQualifiedId ln 124

// NOT DONE
// GetImageId - versioned, use replacements, GetImageInfoJson.GetImageId ln 161
// GetFullImagePath - versioned, use replacements. ThumbsMiddleware.GetFullImagePath ln 183

/*
 * Have I broken Thumbs handling by having 1 generic setting in parameterStore?
 */