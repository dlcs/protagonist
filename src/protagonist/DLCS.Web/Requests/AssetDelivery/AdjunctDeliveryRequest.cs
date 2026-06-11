namespace DLCS.Web.Requests.AssetDelivery;

/// <summary>
/// Model for a request made for a DLCS asset adjunct
/// </summary>
public class AdjunctDeliveryRequest : BaseAssetRequest
{
    /// <summary>
    /// Id of the requested adjunct
    /// </summary>
    public string? AdjunctId { get; set; }
}
