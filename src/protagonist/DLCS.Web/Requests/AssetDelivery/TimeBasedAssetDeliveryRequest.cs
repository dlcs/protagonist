namespace DLCS.Web.Requests.AssetDelivery;

/// <summary>
/// Model for a request made for a DLCS AV asset
/// </summary>
public class TimeBasedAssetDeliveryRequest : BaseAssetRequest
{
    /// <summary>
    /// Get the ImageRequest equivalent for Timebased media
    /// </summary>
    /// <remarks>This is not IIIF standard</remarks>
    public string TimeBasedRequest { get; set; }
}