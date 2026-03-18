using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.PathElements;

namespace CleanupHandler.Infrastructure.Messages;

public class AssetUpdatedNotificationRequest
{
    public Asset? AssetBeforeUpdate { get; set; }
    
    public Asset? AssetAfterUpdate { get; set; }

    public CustomerPathElement? CustomerPathElement { get; set; }
}

public static class AssetUpdatedNotificationRequestX
{
    public static UpdatedNotificationRequest<Asset> ConvertToStandard(
        this AssetUpdatedNotificationRequest assetUpdatedNotificationRequest)
        => new()
        {
            DeliverableBeforeUpdate = assetUpdatedNotificationRequest.AssetBeforeUpdate,
            DeliverableAfterUpdate = assetUpdatedNotificationRequest.AssetAfterUpdate,
            CustomerPathElement = assetUpdatedNotificationRequest.CustomerPathElement
        };
}
