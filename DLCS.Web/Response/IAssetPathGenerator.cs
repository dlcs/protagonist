using DLCS.Web.Requests.AssetDelivery;

namespace DLCS.Web.Response
{
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
        /// <param name="assetRequest"></param>
        /// <returns></returns>
        string GetFullPathForRequest(BaseAssetRequest assetRequest);
    }
}