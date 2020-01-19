using IIIF.ImageApi;

namespace DLCS.Web.Requests.AssetDelivery
{
    public class ThumbnailRequest : BaseAssetRequest
    {
        public ImageRequest IIIFImageRequest { get; set; }
    }
}
