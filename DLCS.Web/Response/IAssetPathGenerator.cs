using DLCS.Web.Requests.AssetDelivery;

namespace DLCS.Web.Response
{
    /// <summary>
    /// Delegate that takes AssetRequest and appropriate host and returns path string
    /// </summary>
    public delegate string PathGenerator(BaseAssetRequest assetRequest, string template);
    
    /// <summary>
    /// Generate paths related to running Dlcs instance.
    /// </summary>
    public interface IAssetPathGenerator
    {
        /// <summary>
        /// Generate path for specified <see cref="BaseAssetRequest"/> excluding host.
        /// </summary>
        string GetPathForRequest(BaseAssetRequest assetRequest);

        /// <summary>
        /// Generate full path for specified <see cref="BaseAssetRequest"/>, including host. 
        /// </summary>
        string GetFullPathForRequest(BaseAssetRequest assetRequest);

        /// <summary>
        /// Generate full path for specified <see cref="BaseAssetRequest"/>, using provided delegate to generate
        /// path element. 
        /// </summary>
        string GetFullPathForRequest(BaseAssetRequest assetRequest, PathGenerator pathGenerator);
    }
}