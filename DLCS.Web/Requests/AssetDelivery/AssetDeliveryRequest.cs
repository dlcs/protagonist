using IIIF.ImageApi;

namespace DLCS.Web.Requests.AssetDelivery
{
    /// <summary>
    /// Model for a request made for a DLCS asset
    /// </summary>
    public class AssetDeliveryRequest : BaseAssetRequest
    {
        public ImageRequest IIIFImageRequest { get; set; }
    }
}
