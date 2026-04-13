using DLCS.Model.Assets;

namespace DLCS.AWS.SNS.Messaging;

public interface IBatchCompletedNotificationSender
{
    /// <summary>
    /// Broadcast batch completed notification
    /// </summary>
    Task SendBatchCompletedMessage(IDeliverableBatch completedBatch, CancellationToken cancellationToken = default);
}