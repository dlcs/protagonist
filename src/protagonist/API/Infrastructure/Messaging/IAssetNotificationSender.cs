using System.Collections.Generic;

namespace API.Infrastructure.Messaging;

public interface IAssetNotificationSender
{
    /// <summary>
    /// Broadcast a change to the status of an Asset, for any subscribers.
    /// </summary>
    Task SendAssetModifiedMessage(AssetModificationRecord notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast a change to the status of multiple Assets, for any subscribers.
    /// </summary>
    Task SendAssetModifiedMessage(IReadOnlyCollection<AssetModificationRecord> notifications,
        CancellationToken cancellationToken = default);
}