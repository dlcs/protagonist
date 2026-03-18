using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.PathElements;

namespace CleanupHandler.Infrastructure.Messages;

public class AssetDeletedNotificationRequest
{
    public Asset? Asset { get; set; }

    public CustomerPathElement? CustomerPathElement { get; set; }

    public ImageCacheType DeleteFrom { get; set; }
}

public static class AssetDeletedNotificationRequestX
{
    public static DeletedNotificationRequest<Asset> ConvertToStandard(
        this AssetDeletedNotificationRequest assetUpdatedNotificationRequest)
        => new()
        {
            Deliverable = assetUpdatedNotificationRequest.Asset,
            CustomerPathElement = assetUpdatedNotificationRequest.CustomerPathElement,
            DeleteFrom = assetUpdatedNotificationRequest.DeleteFrom
        };
}
