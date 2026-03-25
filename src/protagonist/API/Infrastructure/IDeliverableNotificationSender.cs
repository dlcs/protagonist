using System.Collections.Generic;
using API.Infrastructure.Messaging.General;
using DLCS.Model.Assets;

namespace API.Infrastructure;

public interface IDeliverableNotificationSender
{
    /// <summary>
    /// Broadcast a change to the status of an deliverable, for any subscribers.
    /// </summary>
    Task SendDeliverableModifiedMessage<T>(NotificationRecord<T> notification,
        CancellationToken cancellationToken = default) where T : class, IDeliverable;

    /// <summary>
    /// Broadcast a change to the status of multiple deliverables, for any subscribers.
    /// </summary>
    Task SendDeliverableModifiedMessage<T>(IReadOnlyCollection<NotificationRecord<T>> notifications,
        CancellationToken cancellationToken = default) where T : class, IDeliverable;
}
