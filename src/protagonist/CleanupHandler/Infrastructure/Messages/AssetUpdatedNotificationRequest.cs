using DLCS.Model.Messaging;
using DLCS.Model.PathElements;

namespace CleanupHandler.Infrastructure.Messages;

public class AssetUpdatedNotificationRequest
{
    public DLCS.Model.Assets.Asset? AssetBeforeUpdate { get; set; }
    
    public DLCS.Model.Assets.Asset? AssetAfterUpdate { get; set; }

    public CustomerPathElement? CustomerPathElement { get; set; }
}

public static class AssetUpdatedNotificationRequestX
{
    public static DeliverableUpdatedNotification<DLCS.Model.Assets.Asset> ConvertToStandard(
        this AssetUpdatedNotificationRequest assetUpdatedNotificationRequest)
        => new()
        {
            DeliverableBeforeUpdate = assetUpdatedNotificationRequest.AssetBeforeUpdate,
            DeliverableAfterUpdate = assetUpdatedNotificationRequest.AssetAfterUpdate,
            CustomerPathElement = assetUpdatedNotificationRequest.CustomerPathElement
        };
}
