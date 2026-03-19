using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.PathElements;

namespace CleanupHandler.Infrastructure.Messages;

/// <summary>
/// Legacy format used for asset deleted notification requests
/// </summary>
public class AssetDeletedNotificationRequest
{
    public Asset? Asset { get; set; }

    public CustomerPathElement? CustomerPathElement { get; set; }

    public ImageCacheType DeleteFrom { get; set; }
}

/// <summary>
/// Helper methods for an asset deleted notification
/// </summary>
public static class AssetDeletedNotificationRequestX
{
    /// <summary>
    /// Converts an asset deleted notification request to the new deleted notification request
    /// </summary>
    public static DeletedNotificationRequest<Asset> ConvertToNewFormat(
        this AssetDeletedNotificationRequest assetUpdatedNotificationRequest)
        => new()
        {
            Deliverable = assetUpdatedNotificationRequest.Asset,
            CustomerPathElement = assetUpdatedNotificationRequest.CustomerPathElement,
            DeleteFrom = assetUpdatedNotificationRequest.DeleteFrom
        };
}
