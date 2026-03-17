using System.Collections.Generic;
using API.Infrastructure.Messaging.General;

namespace API.Infrastructure.Messaging.Adjunct;

public interface IAdjunctNotificationSender
{
    /// <summary>
    /// Broadcast a change to the status of an Asset, for any subscribers.
    /// </summary>
    Task SendAdjunctModifiedMessage(NotificationRecord<DLCS.Model.Assets.Adjunct> notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast a change to the status of multiple Assets, for any subscribers.
    /// </summary>
    Task SendAdjunctModifiedMessage(IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>> notifications,
        CancellationToken cancellationToken = default);
}
