using System.Collections.Generic;
using API.Infrastructure.Messaging.General;

namespace API.Infrastructure.Messaging.Asset;

public interface IAssetNotificationSender
{
    /// <summary>
    /// Broadcast a change to the status of an Asset, for any subscribers.
    /// </summary>
    Task SendAssetModifiedMessage(NotificationRecord<DLCS.Model.Assets.Asset> notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast a change to the status of multiple Assets, for any subscribers.
    /// </summary>
    Task SendAssetModifiedMessage(IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Asset>> notifications,
        CancellationToken cancellationToken = default);
}
