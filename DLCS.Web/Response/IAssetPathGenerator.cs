using DLCS.Web.Requests.AssetDelivery;

namespace DLCS.Web.Response
{
    /// <summary>
    /// Generate paths related to running Dlcs instance.
    /// </summary>
    public interface IAssetPathGenerator
    {
        /// <summary>
        /// Generate path for specified assetRequest.
        /// </summary>
        string GetPathForRequest(BaseAssetRequest assetRequest);
    }
}