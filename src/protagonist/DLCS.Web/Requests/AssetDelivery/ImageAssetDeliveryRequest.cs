using IIIF.ImageApi;

namespace DLCS.Web.Requests.AssetDelivery;

/// <summary>
/// Model for a request made for a DLCS image asset
/// </summary>
public class ImageAssetDeliveryRequest : BaseAssetRequest
{
    /// <summary>
    /// IIIF image request
    /// </summary>
    public ImageRequest IIIFImageRequest { get; set; }
}
